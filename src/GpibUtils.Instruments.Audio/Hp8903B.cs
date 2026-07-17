using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using GpibUtils.Visa;
using GpibUtils.Visa.Srq;

namespace GpibUtils.Instruments.Audio
{
    /// <summary>
    /// Driver for the HP 8903B Audio Analyzer — a 1985-era stand-alone audio source + voltmeter + distortion
    /// analyzer + counter with a legacy keystroke-mnemonic HP-IB language (no <c>*IDN?</c>/<c>*OPC</c>/
    /// <c>*ESE</c>/<c>*SRE</c>). Reconstructed from the 8903B Operation &amp; Calibration Manual, Section 3
    /// (issue #131); runs over any <see cref="IInstrumentSession"/>. First implementer of
    /// <see cref="IAudioAnalyzer"/>.
    ///
    /// <para><b>Completion — a #96 consumer.</b> There is no <c>*SRE</c>: the SRQ enable is Special Function 22
    /// ("Service Request Condition", <c>22.N SP</c>, N = data-ready 1 + HP-IB-error 2 + instrument-error 4).
    /// <see cref="Measure"/> arms Hold (<c>T1</c>), then the shared <see cref="CompletionWaiter"/> SRQ-edge
    /// flow enables Data-Ready SRQ via <c>22.{mask}SP</c> (a custom, non-<c>*SRE</c> command), fires the
    /// settled trigger (<c>T3</c>), waits for the data-ready service request, and reads the 12-byte result.</para>
    ///
    /// <para><b>Bench caveat (verify).</b> The manual warns that a serial poll on this instrument BOTH reads
    /// the status byte AND re-triggers a measurement (returning it to Hold). The shared waiter polls the
    /// status byte in a loop, so bench verification must confirm this poll cadence works here; if the
    /// re-trigger interferes, switch this driver to a wait-for-SRQ-line + single-poll sequence. Timeouts stay
    /// generous for HP-IB bus-extender latency.</para>
    /// </summary>
    public sealed class Hp8903B : IAudioAnalyzer
    {
        /// <summary>GPIB address of the 8903B — the manual factory default is 28 decimal (set by internal DIP
        /// switches, not settable over the bus). Override with <c>--address</c>; confirm via Special Function
        /// 21 (<c>21.1SP</c>) or the internal switches. Never trust bus-scan discovery behind HP-IB extenders.</summary>
        public const string DefaultResource = "GPIB0::28::INSTR";

        /// <summary>Status-byte bit weights (8903B Section 3-7). RQS is the standard GPIB bit 0x40.</summary>
        private const int DataReadyBit = 1, HpibErrorBit = 2, InstrumentErrorBit = 4, RequestServiceBit = 64;

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        /// <summary>Backstop for the measurement-complete wait, ms.</summary>
        public int MeasureTimeoutMs { get; set; } = 30000;

        /// <summary>Status-poll interval while waiting for completion, ms. Kept generous — on hardware each
        /// serial poll re-triggers a measurement, so a slow cadence is preferable.</summary>
        public int PollIntervalMs { get; set; } = 250;

        /// <summary>Optional per-poll trace sink (forwarded to the <see cref="CompletionWaiter"/>).</summary>
        public Action<string> Trace { get; set; }

        public Hp8903B(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;

        /// <summary>Every program string sent through the driver, in order (for CLI echo / tests).</summary>
        public IReadOnlyList<string> History => _history;

        private void Send(string command)
        {
            _session.Write(command);
            _history.Add(command);
        }

        /// <summary>The 8903B has no <c>*IDN?</c>; returns a fixed descriptor (identify via the front panel).</summary>
        public string Identify() => "HP 8903B Audio Analyzer (no *IDN?)";

        public void Initialize()
        {
            _session.Clear();   // HP-IB device clear
            Send("AU");         // Automatic Operation — resets special functions to a known state
        }

        public void SetSourceFrequencyHz(double hertz) =>
            Send("FR" + hertz.ToString("0.######", CultureInfo.InvariantCulture) + "HZ");

        public void SetSourceAmplitude(double value, AudioAmplitudeUnit unit) =>
            Send("AP" + value.ToString("0.######", CultureInfo.InvariantCulture) + UnitCode(unit));

        public void SetMeasurement(AudioMeasurement measurement) => Send(MeasurementCode(measurement));

        public void SetDetector(AudioDetector detector) => Send(detector == AudioDetector.Rms ? "A0" : "A1");

        private static string UnitCode(AudioAmplitudeUnit unit)
        {
            switch (unit)
            {
                case AudioAmplitudeUnit.Volts: return "V";
                case AudioAmplitudeUnit.Millivolts: return "MV";
                case AudioAmplitudeUnit.Dbm: return "DV";
                default: return "V";
            }
        }

        private static string MeasurementCode(AudioMeasurement m)
        {
            switch (m)
            {
                case AudioMeasurement.AcLevel: return "M1";
                case AudioMeasurement.Sinad: return "M2";
                case AudioMeasurement.Distortion: return "M3";
                case AudioMeasurement.DcLevel: return "S1";
                case AudioMeasurement.SignalToNoise: return "S2";
                case AudioMeasurement.DistortionLevel: return "S3";
                default: return "M1";
            }
        }

        /// <summary>
        /// Arms Hold (<c>T1</c>) then runs one settled measurement: the shared <see cref="CompletionWaiter"/>
        /// enables Data-Ready SRQ (<c>22.{mask}SP</c>), fires the settled trigger (<c>T3</c>), waits for the
        /// data-ready service request, then the 12-byte result is read and parsed.
        /// </summary>
        public double Measure()
        {
            Send("T1");   // Hold — arm for a single triggered measurement
            var channel = new SessionStatusChannel(_session);
            var sw = Stopwatch.StartNew();
            var result = CompletionWaiter.Wait(
                StatusModel(), "HP8903B", "measurement", MeasureTimeoutMs,
                channel, () => sw.ElapsedMilliseconds, Thread.Sleep, PollIntervalMs, Trace);

            switch (result.Outcome)
            {
                case CompletionOutcome.Completed:
                    _history.Add("<read>");
                    return ParseReading(_session.ReadString());
                case CompletionOutcome.InstrumentError:
                    throw Hp8903BException.InstrumentError(result.Message);
                default:
                    _session.Clear();
                    throw Hp8903BException.Timeout(result.Message);
            }
        }

        /// <summary>
        /// Parses the 8903B 12-byte output (<c>sDDDDDEsNN</c>, signed 5-digit mantissa + signed exponent — a
        /// standard scientific literal). A magnitude ≥ 9×10⁹ is the instrument's error-output encoding.
        /// </summary>
        internal static double ParseReading(string raw)
        {
            var text = (raw ?? string.Empty).Trim();
            if (text.Length == 0)
                throw new FormatException("Empty 8903B output.");
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                throw new FormatException($"Unparseable 8903B output: '{raw}'.");
            if (Math.Abs(value) >= 9e9)
                throw new Hp8903BException($"instrument returned an error value ({text}).", isTimeout: false);
            return value;
        }

        /// <summary>
        /// The 8903B's measurement-complete status model for the SRQ-edge <see cref="CompletionWaiter"/> flow:
        /// Special Function 22 (<c>22.{mask}SP</c>) as the SRQ enable (there is no <c>*SRE</c> register), reset
        /// to the power-up default <c>22.2SP</c> on clear, and the 8903B status-byte bit table (data-ready =
        /// 0x01, HP-IB error = 0x02, instrument error = 0x04, RQS = 0x40). The settled trigger (<c>T3</c>) is
        /// the arm. Kept here so it can move to the #41 instrument DB unchanged.
        /// </summary>
        internal static StatusModel StatusModel() => new StatusModel
        {
            SrqSupported = true,
            SerialPoll = new SerialPollSpec { ClearsRqs = true },
            EnableMask = new EnableMaskSpec { SetCommand = "22.{mask}SP", ClearCommand = "22.2SP" },
            ErrorBit = "instrumentError",
            RequestServiceBit = "requestService",
            Bits = new Dictionary<string, int>
            {
                ["dataReady"] = DataReadyBit,
                ["hpibError"] = HpibErrorBit,
                ["instrumentError"] = InstrumentErrorBit,
                ["requestService"] = RequestServiceBit
            },
            Operations = new Dictionary<string, StatusOperation>
            {
                ["measurement"] = new StatusOperation { Arm = "T3", ExpectBit = "dataReady" }
            }
        };
    }
}
