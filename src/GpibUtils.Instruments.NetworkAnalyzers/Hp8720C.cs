using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.NetworkAnalyzers
{
    /// <summary>
    /// Driver for the HP 8720C Microwave Vector Network Analyzer (50 MHz–20 GHz) — a legacy front-panel-derived
    /// mnemonic HP-IB language (semicolon-terminated) that is also IEEE-488.2-compliant (<c>*IDN?</c>/<c>*OPC?</c>).
    /// Sets the sweep, source power and S-parameter, triggers a single sweep blocking on <c>*OPC?</c>, and reads
    /// the formatted trace + peak marker. Reconstructed from the 8720C User's Manual (issue #128). Runs over any
    /// <see cref="IInstrumentSession"/>.
    ///
    /// <para>Formatted-data (<c>OUTPFORM</c>) binary layouts (FORM1-5) are documented only in Programming Guides
    /// not in the Manuals folder; this driver uses ASCII (<c>FORM4</c>). The <c>OUTPMARK</c> field order and any
    /// value/aux pairing are bench-confirm items.</para>
    /// </summary>
    public sealed class Hp8720C : INetworkAnalyzer
    {
        /// <summary>GPIB address of the 8720C — the manual's examples address it at 16. Override with
        /// <c>--address</c>.</summary>
        public const string DefaultResource = "GPIB0::16::INSTR";

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public Hp8720C(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;
        public IReadOnlyList<string> History => _history;

        /// <summary>Backstop for the single-sweep <c>*OPC?</c> wait, ms.</summary>
        public int SweepTimeoutMs { get; set; } = 60000;

        private void Send(string command) { _session.Write(command); _history.Add(command); }
        private string Query(string command) { _history.Add(command); return (_session.Query(command) ?? string.Empty).Trim(); }

        public string Identify() => Query("*IDN?");
        public void Initialize() { _session.Clear(); Send("PRES"); }

        public void SetStartFrequencyHz(double hertz) =>
            Send("STAR " + hertz.ToString("0.######", CultureInfo.InvariantCulture) + " HZ");

        public void SetStopFrequencyHz(double hertz) =>
            Send("STOP " + hertz.ToString("0.######", CultureInfo.InvariantCulture) + " HZ");

        public void SetSourcePowerDbm(double dbm) =>
            Send("POWE " + dbm.ToString("0.###", CultureInfo.InvariantCulture));

        public void SetSweepPoints(int points) => Send("POIN " + points.ToString(CultureInfo.InvariantCulture));

        /// <summary>Selects the S-parameter the active channel measures (<c>S11</c>/<c>S21</c>/<c>S12</c>/
        /// <c>S22</c>). The raw A/B/R inputs are not the 8720C's model — use an S-parameter.</summary>
        public void SetMeasurement(NetworkParameter parameter)
        {
            switch (parameter)
            {
                case NetworkParameter.S11: Send("S11"); break;
                case NetworkParameter.S21: Send("S21"); break;
                case NetworkParameter.S12: Send("S12"); break;
                case NetworkParameter.S22: Send("S22"); break;
                default: throw new NotSupportedException($"The 8720C measures S-parameters, not {parameter}.");
            }
        }

        public void SingleSweep()
        {
            Send("SING");
            int prior = _session.TimeoutMilliseconds;
            try { _session.TimeoutMilliseconds = SweepTimeoutMs; Query("*OPC?"); }
            finally { _session.TimeoutMilliseconds = prior; }
        }

        public IReadOnlyList<double> ReadFormattedTrace()
        {
            Send("FORM4");   // ASCII output format
            return NetworkAnalyzerParsing.ParseArray(Query("OUTPFORM"), "OUTPFORM");
        }

        public double MarkerToPeakY()
        {
            Send("MARK1");
            Send("SEAMAX");   // marker search: maximum
            return NetworkAnalyzerParsing.ParseScalar(Query("OUTPMARK"), "OUTPMARK");   // field 0 = marker value
        }

        /// <summary>Reads the active marker's stimulus (<c>OUTPMARK</c> returns value1,value2,stimulus).</summary>
        public double MarkerFrequencyHz()
        {
            var fields = Query("OUTPMARK").Split(',');
            var stimulus = (fields.Length >= 3 ? fields[2] : fields[fields.Length - 1]).Trim();
            if (!double.TryParse(stimulus, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                throw new FormatException($"Unrecognized 8720C OUTPMARK stimulus field: '{stimulus}'.");
            return v;
        }
    }
}
