using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.NetworkAnalyzers
{
    /// <summary>
    /// Driver for the HP 8757D/E Scalar Network Analyzer — a pre-SCPI HP-IB instrument driven by two-letter
    /// program codes (compatible with the HP 8757A/C/E code set; no <c>*IDN?</c>/<c>*RST</c>). Implements
    /// <see cref="INetworkAnalyzer"/> (issue #129) with the scalar caveats below. Reconstructed from the 8757D
    /// User's Guide programming-code table (HP P/N 08757-90130). Runs over any <see cref="IInstrumentSession"/>.
    ///
    /// <para><b>Scalar analyzer, not a source.</b> The 8757D measures absolute power (dBm) and ratios from
    /// detectors A/B/R; it does <b>not</b> set its own frequency — the swept source drives frequency over the
    /// dedicated 8757 SYSTEM INTERFACE port, and <c>FA</c>/<c>FB</c> are display <i>labels</i>. So
    /// <see cref="SetStartFrequencyHz"/>/<see cref="SetStopFrequencyHz"/> set labels (and are remembered to
    /// scale the marker frequency host-side), <see cref="SetSourcePowerDbm"/> is a no-op (set power on the
    /// source), and <see cref="SetMeasurement"/> maps S11→detector-A (reflection) / S21→detector-B
    /// (transmission) absolute power — the true A/R, B/R ratio codes and the exact <c>FA</c>/<c>FB</c> numeric
    /// syntax live in the Programming Guide (08757-90109) and are bench-confirm items. The peak marker is
    /// computed host-side from the formatted trace (no cursor-search code invented).</para>
    /// </summary>
    public sealed class Hp8757D : INetworkAnalyzer
    {
        /// <summary>GPIB address of the 8757D — factory default 16 (firmware default; survives preset).
        /// Override with <c>--address</c>.</summary>
        public const string DefaultResource = "GPIB0::16::INSTR";

        /// <summary>Point counts the 8757D accepts (<c>SP</c> code).</summary>
        public static readonly int[] ValidPointCounts = { 101, 201, 401, 801, 1601 };

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();
        private double _startHz = double.NaN;
        private double _stopHz = double.NaN;
        private int _points = 401;
        private int _lastPeakIndex = -1;

        public Hp8757D(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;
        public IReadOnlyList<string> History => _history;

        private void Send(string command) { _session.Write(command); _history.Add(command); }
        private string Query(string command) { _history.Add(command); return (_session.Query(command) ?? string.Empty).Trim(); }

        /// <summary>Reads the instrument identity (<c>OI</c>, not <c>*IDN?</c>).</summary>
        public string Identify() => Query("OI");

        /// <summary>Device clear + instrument preset (<c>IP</c>).</summary>
        public void Initialize() { _session.Clear(); Send("IP"); }

        /// <summary>Sets the start-frequency display label (<c>FA</c>) and remembers it for marker scaling.
        /// The actual sweep range is set on the swept source, not the 8757D — numeric syntax is bench-confirm.</summary>
        public void SetStartFrequencyHz(double hertz)
        {
            _startHz = hertz;
            Send("FA" + hertz.ToString("0.######", CultureInfo.InvariantCulture));
        }

        /// <summary>Sets the stop-frequency display label (<c>FB</c>) and remembers it for marker scaling.</summary>
        public void SetStopFrequencyHz(double hertz)
        {
            _stopHz = hertz;
            Send("FB" + hertz.ToString("0.######", CultureInfo.InvariantCulture));
        }

        /// <summary>No-op on the 8757D — it is a scalar detector, not a source. Set the output power on the
        /// swept source instead. Recorded in <see cref="History"/> for visibility.</summary>
        public void SetSourcePowerDbm(double dbm) =>
            _history.Add("(source power " + dbm.ToString("0.###", CultureInfo.InvariantCulture) +
                         " dBm ignored: 8757D is a scalar detector — set it on the source)");

        /// <summary>Sets the number of sweep points (<c>SP</c>): one of 101/201/401/801/1601.</summary>
        public void SetSweepPoints(int points)
        {
            if (Array.IndexOf(ValidPointCounts, points) < 0)
                throw new ArgumentOutOfRangeException(nameof(points), points, "8757D points must be 101, 201, 401, 801 or 1601.");
            _points = points;
            Send("SP" + points.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>Selects the measured parameter. Scalar mapping: <c>S11</c>→detector A absolute power
        /// (<c>IA</c>, reflection), <c>S21</c>→detector B absolute power (<c>IB</c>, transmission); the raw
        /// inputs map directly (<c>IA</c>/<c>IB</c>/<c>IR</c>). Reverse parameters (<c>S12</c>/<c>S22</c>) have
        /// no scalar single-connection measurement and are unsupported.</summary>
        public void SetMeasurement(NetworkParameter parameter)
        {
            switch (parameter)
            {
                case NetworkParameter.S11:
                case NetworkParameter.InputA: Send("IA"); break;
                case NetworkParameter.S21:
                case NetworkParameter.InputB: Send("IB"); break;
                case NetworkParameter.InputR: Send("IR"); break;
                default:
                    throw new NotSupportedException(
                        $"The 8757D is a scalar analyzer: {parameter} has no single-connection measurement. " +
                        "Use S11/S21 or InputA/InputB/InputR.");
            }
        }

        /// <summary>Triggers one sweep and holds (<c>SV1</c> = take one sweep then hold), clearing status first.
        /// Sweep-complete asserts SRQ (maps onto the shared Srq engine — a follow-up for async completion).</summary>
        public void SingleSweep() { Send("CS"); Send("SV1"); }

        /// <summary>Reads the formatted trace as ASCII (<c>FD0</c> then <c>OD</c>, output trace data).</summary>
        public IReadOnlyList<double> ReadFormattedTrace()
        {
            Send("FD0");
            return ParseTrace(Query("OD"));
        }

        /// <summary>Returns the trace peak (formatted units, e.g. dB) — computed host-side from the trace, and
        /// remembers the peak index so <see cref="MarkerFrequencyHz"/> can scale it (the 8757D's own
        /// cursor-search code is a Programming-Guide/bench item, so it is not used here).</summary>
        public double MarkerToPeakY()
        {
            var trace = ReadFormattedTrace();
            if (trace.Count == 0) throw new InvalidOperationException("8757D returned an empty trace.");
            var peak = trace[0];
            _lastPeakIndex = 0;
            for (int i = 1; i < trace.Count; i++)
                if (trace[i] > peak) { peak = trace[i]; _lastPeakIndex = i; }
            return peak;
        }

        /// <summary>Returns the peak marker's stimulus frequency, scaled host-side from the remembered
        /// start/stop labels and point count (the 8757D does not own the frequency axis — the source does).</summary>
        public double MarkerFrequencyHz()
        {
            if (_lastPeakIndex < 0) throw new InvalidOperationException("Call MarkerToPeakY() before MarkerFrequencyHz().");
            if (double.IsNaN(_startHz) || double.IsNaN(_stopHz))
                throw new InvalidOperationException("Set start/stop frequency labels before reading marker frequency.");
            int last = Math.Max(1, _points - 1);
            return _startHz + (_stopHz - _startHz) * _lastPeakIndex / last;
        }

        internal static IReadOnlyList<double> ParseTrace(string raw)
        {
            var list = new List<double>();
            if (string.IsNullOrWhiteSpace(raw)) return list;
            foreach (var tok in raw.Split(new[] { ',', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                if (double.TryParse(tok, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    list.Add(v);
            return list;
        }
    }
}
