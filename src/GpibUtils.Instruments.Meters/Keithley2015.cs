using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.Meters
{
    /// <summary>
    /// Driver for the Keithley 2015/2015P THD Multimeter — a SCPI (IEEE-488.2) DMM in the Keithley 2000
    /// family. Implements the standard <see cref="IDigitalMultimeter"/> surface (CONFigure / READ? / FUNC?)
    /// over any <see cref="IInstrumentSession"/> (issue #133). The 2015's audio-distortion functions
    /// (THD/THD+N/SINAD) and the 2015P peak-search spectrum commands are a follow-up (P1 #94
    /// IAudioDistortionAnalyzer); this driver covers the DMM measurement surface.
    /// </summary>
    public sealed class Keithley2015 : IDigitalMultimeter
    {
        /// <summary>GPIB address of the 2015 — factory default 16 (Keithley convention). Override with
        /// <c>--address</c>.</summary>
        public const string DefaultResource = "GPIB0::16::INSTR";

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public Keithley2015(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;
        public IReadOnlyList<string> History => _history;

        private void Send(string command) { _session.Write(command); _history.Add(command); }
        private string Query(string command) { _history.Add(command); return (_session.Query(command) ?? string.Empty).Trim(); }

        public string Identify() => Query("*IDN?");
        public void Initialize() { _session.Clear(); Send("*CLS"); Send("*RST"); }
        public void Reset() => Send("*RST");

        public void Configure(MeasurementFunction function, string range = null, string resolution = null)
        {
            var cmd = "CONF:" + function.Root();
            if (function.IsRangeable() && !string.IsNullOrWhiteSpace(range))
            {
                cmd += " " + range.Trim();
                if (!string.IsNullOrWhiteSpace(resolution)) cmd += "," + resolution.Trim();
            }
            Send(cmd);
        }

        public string QueryConfiguration() => Query("CONF?");
        public string QueryFunction() => Query("FUNC?");

        private static void RequireRangeable(MeasurementFunction function)
        {
            if (!function.IsRangeable())
                throw new ArgumentOutOfRangeException(nameof(function), function, $"{function} has no range/NPLC.");
        }

        public void SetAutoRange(MeasurementFunction function, bool on)
        { RequireRangeable(function); Send($"{function.Root()}:RANG:AUTO {(on ? "ON" : "OFF")}"); }

        public void SetNplc(MeasurementFunction function, double nplc)
        { RequireRangeable(function); Send($"{function.Root()}:NPLC {nplc.ToString("G7", CultureInfo.InvariantCulture)}"); }

        public void SetTriggerSource(TriggerSource source)
        {
            string arg;
            switch (source)
            {
                case TriggerSource.Immediate: arg = "IMM"; break;
                case TriggerSource.Bus: arg = "BUS"; break;
                case TriggerSource.External: arg = "EXT"; break;
                default: throw new ArgumentOutOfRangeException(nameof(source), source, null);
            }
            Send($"TRIG:SOUR {arg}");
        }

        public void SetTriggerCount(int count)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count), count, "Trigger count must be >= 1.");
            Send($"TRIG:COUN {count.ToString(CultureInfo.InvariantCulture)}");
        }

        public void SetSampleCount(int count)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count), count, "Sample count must be >= 1.");
            Send($"SAMP:COUN {count.ToString(CultureInfo.InvariantCulture)}");
        }

        public void Initiate() => Send("INIT");
        public void BusTrigger() => Send("*TRG");

        public double ReadValue() => ParseReading(Query("READ?"));

        public double[] ReadValues(int count)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count), count, "Count must be >= 1.");
            SetSampleCount(count);
            return ParseReadingList(Query("READ?"));
        }

        public double[] Fetch() => ParseReadingList(Query("FETCh?"));

        public string NextError() => Query("SYST:ERR?");

        public IReadOnlyList<string> DrainErrors()
        {
            var errors = new List<string>();
            for (int i = 0; i < 64; i++)
            {
                var e = NextError();
                if (string.IsNullOrEmpty(e)) break;
                var code = e.Split(',')[0].Trim();
                if (code == "0" || code == "+0") break;
                errors.Add(e);
            }
            return errors;
        }

        public bool SelfTest() => Query("*TST?").Trim().StartsWith("0");

        internal static double ParseReading(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) throw new FormatException("Empty Keithley 2015 reading.");
            // A reading may arrive as "<value>" or a comma-list ("<value>,<time>,<reading#>"); take the first field.
            var first = raw.Split(',')[0].Trim();
            if (!double.TryParse(first, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                throw new FormatException($"Unrecognized Keithley 2015 reading: '{raw}'.");
            return v;
        }

        internal static double[] ParseReadingList(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) throw new FormatException("Empty Keithley 2015 reading list.");
            var parts = raw.Split(',');
            var list = new List<double>();
            foreach (var p in parts)
                if (double.TryParse(p.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    list.Add(v);
            if (list.Count == 0) throw new FormatException($"No parseable values in Keithley 2015 reading: '{raw}'.");
            return list.ToArray();
        }
    }
}
