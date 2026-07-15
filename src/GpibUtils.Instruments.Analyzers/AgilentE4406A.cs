using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.Analyzers
{
    /// <summary>Channel-power scalar result: total power (dBm) and power spectral density (dBm/Hz).</summary>
    public struct ChannelPowerResult
    {
        public double TotalPowerDbm { get; set; }
        public double PowerSpectralDensityDbmHz { get; set; }

        /// <summary>The full raw scalar set returned by the analyzer (may be longer than the two named fields).</summary>
        public double[] Raw { get; set; }
    }

    /// <summary>
    /// Driver for the Agilent E4406A VSA Transmitter Tester (Basic mode) — a SCPI measurement instrument.
    /// Selects Basic single-measurement mode at a center frequency and runs a named measurement via the
    /// four SCPI verbs (<c>:MEASure</c>/<c>:CONFigure</c>/<c>:READ</c>/<c>:FETCh</c>), returning the comma-
    /// separated scalar set. Ported from the <c>ESG-SignalCreator.Core/Measure</c> layer (issue #12); the
    /// app's typed-result / multi-model dialect layer stays upstream — this driver is the direct E4406A
    /// control surface. Runs over any <see cref="IInstrumentSession"/>.
    ///
    /// <para><b>Completion:</b> the E4406A returns each measurement on a blocking <c>:READ:&lt;root&gt;?</c>
    /// query (no armed-mask SRQ flow), so the #43 SRQ engine is not used; keep the session timeout generous
    /// for a long measurement or an auto-alignment.</para>
    ///
    /// <para>The E4406A has <b>no global span</b> — span, where settable, is per-measurement
    /// (<c>:SENSe:CHPower:FREQuency:SPAN</c>). Basic-mode ACP has no settable span (hardware-truthed on
    /// FW A.08.10), only an integration bandwidth.</para>
    /// </summary>
    public sealed class AgilentE4406A
    {
        /// <summary>GPIB address of the E4406A — the manual's factory-default HP-IB address is 18. Override
        /// with <c>--address</c>. Note: the legacy app used bench value <b>17</b>; configure the real bench
        /// address via <c>config address set e4406a …</c> rather than relying on this fallback. Never trust
        /// bus-scan discovery on this bench (HP-IB extenders make every address look present).</summary>
        public const string DefaultResource = "GPIB0::18::INSTR";

        // Measurement roots (SCPI short forms, portable across the E4406A and N9010A).
        public const string ChannelPowerRoot = "CHPower";
        public const string AcpRoot = "ACP";
        public const string CcdfRoot = "PSTatistic";
        public const string WaveformRoot = "WAVeform";
        public const string SpectrumRoot = "SPECtrum";

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public AgilentE4406A(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;

        /// <summary>Every command sent through the driver, in order (for CLI echo / tests).</summary>
        public IReadOnlyList<string> History => _history;

        private void Send(string command)
        {
            _session.Write(command);
            _history.Add(command);
        }

        private string Query(string command)
        {
            _history.Add(command);
            return (_session.Query(command) ?? string.Empty).Trim();
        }

        public string Identify() => Query("*IDN?");

        public void Initialize()
        {
            _session.Clear();
            Send("*RST");
            Send("*CLS");
            SelectBasicMode();
            SetSingleMeasurement();
        }

        public void Reset()
        {
            Send("*RST");
            Send("*CLS");
        }

        /// <summary>Selects the Basic measurement personality (<c>:INSTrument:SELect BASIC</c>).</summary>
        public void SelectBasicMode() => Send(":INSTrument:SELect BASIC");

        /// <summary>Single-measurement mode (<c>:INITiate:CONTinuous OFF</c>).</summary>
        public void SetSingleMeasurement() => Send(":INITiate:CONTinuous OFF");

        /// <summary>Continuous-measurement mode (<c>:INITiate:CONTinuous ON</c>).</summary>
        public void SetContinuous() => Send(":INITiate:CONTinuous ON");

        /// <summary>Sets the analyzer center frequency in Hz (<c>:SENSe:FREQuency:CENTer &lt;hz&gt; Hz</c>).</summary>
        public void SetCenterFrequencyHz(double hertz) =>
            Send(":SENSe:FREQuency:CENTer " + hertz.ToString("G17", CultureInfo.InvariantCulture) + " Hz");

        /// <summary>Sets the analyzer center frequency in MHz.</summary>
        public void SetCenterFrequencyMHz(double mhz) => SetCenterFrequencyHz(mhz * 1e6);

        /// <summary>Reads the analyzer center frequency in Hz (<c>:SENSe:FREQuency:CENTer?</c>).</summary>
        public double GetCenterFrequencyHz() => ParseScalar(Query(":SENSe:FREQuency:CENTer?"), "center frequency");

        // ---- measurement verbs ---------------------------------------------

        /// <summary>Reads a measurement honoring persistent settings (<c>:READ:&lt;root&gt;[n]?</c>), parsed
        /// to a scalar array. <paramref name="n"/> ≤ 1 selects the scalar result set (no index appended).</summary>
        public double[] Read(string root, int n = 1) => ParseScalars(Query(Verb(":READ:", root, n)));

        /// <summary>One-shot measurement with factory defaults (<c>:MEASure:&lt;root&gt;[n]?</c>).</summary>
        public double[] Measure(string root, int n = 1) => ParseScalars(Query(Verb(":MEASure:", root, n)));

        /// <summary>Returns data from the most recent measurement without re-measuring
        /// (<c>:FETCh:&lt;root&gt;[n]?</c>).</summary>
        public double[] Fetch(string root, int n = 1) => ParseScalars(Query(Verb(":FETCh:", root, n)));

        /// <summary>Sets up a measurement to defaults and goes to single mode without initiating
        /// (<c>:CONFigure:&lt;root&gt;</c>).</summary>
        public void Configure(string root) => Send(":CONFigure:" + root);

        private static string Verb(string prefix, string root, int n)
        {
            if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("A measurement root is required.", nameof(root));
            string index = n <= 1 ? string.Empty : n.ToString(CultureInfo.InvariantCulture);
            return prefix + root + index + "?";
        }

        // ---- typed convenience measurements --------------------------------

        /// <summary>
        /// Runs a Channel Power measurement: Basic single mode at <paramref name="centerHz"/>, optional
        /// per-measurement span / integration bandwidth, then reads the <c>CHPower</c> scalar set
        /// <c>[total power dBm, PSD dBm/Hz]</c>. A missing field yields <see cref="double.NaN"/>.
        /// </summary>
        public ChannelPowerResult MeasureChannelPower(double centerHz, double spanHz = 0, double integrationBandwidthHz = 0)
        {
            SelectBasicMode();
            SetSingleMeasurement();
            SetCenterFrequencyHz(centerHz);
            if (spanHz > 0)
                Send(":SENSe:CHPower:FREQuency:SPAN " + spanHz.ToString("G17", CultureInfo.InvariantCulture) + " Hz");
            if (integrationBandwidthHz > 0)
                Send(":SENSe:CHPower:BANDwidth:INTegration " + integrationBandwidthHz.ToString("G17", CultureInfo.InvariantCulture) + " Hz");

            double[] s = Read(ChannelPowerRoot);
            return new ChannelPowerResult
            {
                Raw = s,
                TotalPowerDbm = s.Length > 0 ? s[0] : double.NaN,
                PowerSpectralDensityDbmHz = s.Length > 1 ? s[1] : double.NaN
            };
        }

        /// <summary>
        /// Runs an Adjacent Channel Power measurement: Basic single mode at <paramref name="centerHz"/>,
        /// optional carrier integration bandwidth, then reads the <c>ACP</c> scalar set. Basic-mode ACP has
        /// no settable span. Returns the raw scalar array.
        /// </summary>
        public double[] MeasureAcp(double centerHz, double integrationBandwidthHz = 0)
        {
            SelectBasicMode();
            SetSingleMeasurement();
            SetCenterFrequencyHz(centerHz);
            if (integrationBandwidthHz > 0)
                Send(":SENSe:ACP:BANDwidth:INTegration " + integrationBandwidthHz.ToString("G17", CultureInfo.InvariantCulture) + " Hz");
            return Read(AcpRoot);
        }

        /// <summary>Reads the head of the SCPI error queue (<c>:SYSTem:ERRor?</c>).</summary>
        public string GetError() => Query(":SYSTem:ERRor?");

        // ---- parsing --------------------------------------------------------

        /// <summary>Splits a comma-separated response into doubles, tolerant of whitespace/quotes; skips
        /// non-numeric fields. Empty/null yields an empty array.</summary>
        internal static double[] ParseScalars(string response)
        {
            var values = new List<double>();
            if (string.IsNullOrEmpty(response)) return values.ToArray();
            foreach (var part in response.Split(','))
            {
                var s = part.Trim().Trim('"');
                if (s.Length == 0) continue;
                if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    values.Add(v);
            }
            return values.ToArray();
        }

        internal static double ParseScalar(string raw, string what)
        {
            var s = ParseScalars(raw);
            if (s.Length == 0) throw new FormatException($"Unrecognized E4406A {what} response: '{raw}'.");
            return s[0];
        }
    }
}
