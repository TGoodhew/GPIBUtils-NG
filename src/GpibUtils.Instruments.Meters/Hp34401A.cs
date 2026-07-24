using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.Meters
{
    /// <summary>
    /// Driver for the HP/Agilent/Keysight 34401A 6½-digit Digital Multimeter — a plain IEEE-488.2 SCPI
    /// instrument. Exposes the measurement surface (CONFigure / SENSe / TRIGger / SAMPle / CALCulate /
    /// DISPlay / status) the two source apps drove — the 5440A-calibrator verification harness
    /// (<c>5440Controller/34401AController</c>, the canonical surface) and the HP 435B recorder-output
    /// test (<c>HP435B-Test</c>, buffered multi-point acquisition). Runs over any
    /// <see cref="IInstrumentSession"/>.
    ///
    /// <para>Reads use plain <c>READ?</c> (INITiate + FETCh) which blocks until the burst completes; for
    /// large sample counts raise the session timeout. This is deliberately simpler than the source's
    /// <c>*OPC</c>/SRQ handshake — the 34401A returns all buffered readings on <c>READ?</c> in one
    /// response, so no service-request wait is needed for a bounded burst.</para>
    /// </summary>
    public sealed class Hp34401A : IDigitalMultimeter
    {
        /// <summary>GPIB address of the 34401A — its documented factory-default GPIB address is 22 (34401A
        /// User's Guide, "GPIB Address", p.91: "The address is set to 22 when the multimeter is shipped from
        /// the factory"). Override with <c>--address</c>. As always on this bench, never rely on bus-scan
        /// discovery — the HP-IB extenders make every address look present; drive by this explicit resource.</summary>
        public const string DefaultResource = "GPIB0::22::INSTR";

        /// <summary>Cap on how many entries <see cref="DrainErrors"/> reads, so a stuck error queue can't loop
        /// forever. The 34401A error queue holds up to 20 entries.</summary>
        private const int ErrorQueueDrainCap = 32;

        /// <summary>The 34401A reply when the error queue is empty.</summary>
        public const string NoError = "+0,\"No error\"";

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public Hp34401A(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;

        /// <summary>Every SCPI command sent through the driver, in order (for CLI echo / tests).</summary>
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

        // ---- identity / lifecycle ----------------------------------------------

        public string Identify() => Query("*IDN?");

        public void Initialize()
        {
            _session.Clear();     // GPIB device clear — drop pending I/O and any latched SRQ
            Send("*RST");         // reset to the power-on measurement state
            Send("*CLS");         // clear status registers + error queue
        }

        public void Reset() => Send("*RST");

        /// <summary>Clears the status registers and error queue (<c>*CLS</c>).</summary>
        public void ClearStatus() => Send("*CLS");

        public bool SelfTest()
        {
            // *TST? runs the internal self-test (a few seconds) and returns 0 on pass. Callers driving real
            // hardware should raise the session timeout before this; the simulator answers immediately.
            var r = Query("*TST?");
            return r == "+0" || r == "0";
        }

        // ---- configuration ------------------------------------------------------

        public void Configure(MeasurementFunction function, string range = null, string resolution = null)
        {
            if (!function.IsRangeable())
            {
                // Continuity / diode take no range or resolution argument.
                Send("CONF:" + function.Root());
                return;
            }
            Send("CONF:" + function.Root() + BuildRangeRes(range, resolution));
        }

        public string QueryConfiguration() => Query("CONF?");

        public string QueryFunction() => Query("FUNC?");

        /// <summary>Sets a manual range for a rangeable function (<c>&lt;func&gt;:RANGe</c>); accepts a numeric
        /// value or <c>MIN</c>/<c>MAX</c>.</summary>
        public void SetRange(MeasurementFunction function, string range)
        {
            RequireRangeable(function);
            if (string.IsNullOrWhiteSpace(range)) throw new ArgumentException("Range is required.", nameof(range));
            Send($"{function.Root()}:RANG {range.Trim()}");
        }

        public void SetAutoRange(MeasurementFunction function, bool on)
        {
            RequireRangeable(function);
            Send($"{function.Root()}:RANG:AUTO {(on ? "ON" : "OFF")}");
        }

        public void SetNplc(MeasurementFunction function, double nplc)
        {
            RequireRangeable(function);
            Send($"{function.Root()}:NPLC {nplc.ToString("G7", CultureInfo.InvariantCulture)}");
        }

        /// <summary>Selects DC input impedance: <c>true</c> = auto (&gt;10 GΩ on the 100 mV / 1 V / 10 V DCV
        /// ranges), <c>false</c> = fixed 10 MΩ on every range (<c>INPut:IMPedance:AUTO</c>).</summary>
        public void SetInputImpedanceAuto(bool on) => Send($"INP:IMP:AUTO {(on ? "ON" : "OFF")}");

        /// <summary>Sets the autozero mode (<c>SENSe:ZERO:AUTO</c>).</summary>
        public void SetAutoZero(AutoZeroMode mode)
        {
            string arg;
            switch (mode)
            {
                case AutoZeroMode.Off: arg = "OFF"; break;
                case AutoZeroMode.Once: arg = "ONCE"; break;
                case AutoZeroMode.On: arg = "ON"; break;
                default: throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
            Send($"ZERO:AUTO {arg}");
        }

        /// <summary>Queries which input terminals are selected (<c>ROUTe:TERMinals?</c>): <c>FRON</c> or <c>REAR</c>.</summary>
        public string QueryTerminals() => Query("ROUT:TERM?");

        // ---- trigger / sample ---------------------------------------------------

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

        /// <summary>Sets the trigger delay in seconds, or enables the automatic delay when
        /// <paramref name="seconds"/> is null (<c>TRIGger:DELay[:AUTO]</c>).</summary>
        public void SetTriggerDelay(double? seconds)
        {
            if (seconds == null) { Send("TRIG:DEL:AUTO ON"); return; }
            if (seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "Delay must be >= 0.");
            Send($"TRIG:DEL {seconds.Value.ToString("G7", CultureInfo.InvariantCulture)}");
        }

        public void Initiate() => Send("INIT");

        public void BusTrigger() => Send("*TRG");

        // ---- reads --------------------------------------------------------------

        public double ReadValue() => ParseReading(Query("READ?"));

        public double[] ReadValues(int count)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count), count, "Count must be >= 1.");
            SetSampleCount(count);
            return ParseReadingList(Query("READ?"));
        }

        public double[] Fetch() => ParseReadingList(Query("FETC?"));

        /// <summary>Fetches a single already-taken reading (<c>FETCh?</c>) as one value.</summary>
        public double FetchValue() => ParseReading(Query("FETC?"));

        // ---- math (CALCulate) ---------------------------------------------------

        /// <summary>A CALCulate math function on the 34401A.</summary>
        public enum MathFunction
        {
            /// <summary>Null (relative) — subtract a stored offset.</summary>
            Null,
            /// <summary>dB relative to a stored reference.</summary>
            Db,
            /// <summary>dBm into the reference resistance.</summary>
            Dbm,
            /// <summary>Statistics (min/max/average/count) over the reading buffer.</summary>
            Average,
            /// <summary>Pass/fail limit testing.</summary>
            Limit
        }

        /// <summary>Selects the math function (<c>CALCulate:FUNCtion</c>).</summary>
        public void SetMathFunction(MathFunction function)
        {
            string arg;
            switch (function)
            {
                case MathFunction.Null: arg = "NULL"; break;
                case MathFunction.Db: arg = "DB"; break;
                case MathFunction.Dbm: arg = "DBM"; break;
                case MathFunction.Average: arg = "AVER"; break;
                case MathFunction.Limit: arg = "LIM"; break;
                default: throw new ArgumentOutOfRangeException(nameof(function), function, null);
            }
            Send($"CALC:FUNC {arg}");
        }

        /// <summary>Enables/disables math processing (<c>CALCulate:STATe</c>).</summary>
        public void EnableMath(bool on) => Send($"CALC:STAT {(on ? "ON" : "OFF")}");

        /// <summary>Sets the null (relative) offset (<c>CALCulate:NULL:OFFSet</c>).</summary>
        public void SetNullOffset(double offset) =>
            Send($"CALC:NULL:OFFS {offset.ToString("G7", CultureInfo.InvariantCulture)}");

        /// <summary>Sets the limit-test bounds (<c>CALCulate:LIMit:LOWer/UPPer</c>).</summary>
        public void SetLimits(double lower, double upper)
        {
            Send($"CALC:LIM:LOW {lower.ToString("G7", CultureInfo.InvariantCulture)}");
            Send($"CALC:LIM:UPP {upper.ToString("G7", CultureInfo.InvariantCulture)}");
        }

        /// <summary>Reads the CALCulate:AVERage statistics accumulated over the reading buffer
        /// (min / max / average / count). Requires the Average math function to be enabled.</summary>
        public (double Min, double Max, double Average, int Count) ReadAverageStatistics()
        {
            double min = ParseReading(Query("CALC:AVER:MIN?"));
            double max = ParseReading(Query("CALC:AVER:MAX?"));
            double avg = ParseReading(Query("CALC:AVER:AVER?"));
            int count = (int)Math.Round(ParseReading(Query("CALC:AVER:COUN?")));
            return (min, max, avg, count);
        }

        // ---- display ------------------------------------------------------------

        /// <summary>Turns the front-panel display on or off (<c>DISPlay</c>).</summary>
        public void SetDisplayEnabled(bool on) => Send($"DISP {(on ? "ON" : "OFF")}");

        /// <summary>Shows up to 12 characters on the display (<c>DISPlay:TEXT</c>). Embedded quotes are stripped.</summary>
        public void SetDisplayText(string text)
        {
            var t = (text ?? string.Empty).Replace("\"", "");
            if (t.Length > 12) t = t.Substring(0, 12);
            Send($"DISP:TEXT \"{t}\"");
        }

        /// <summary>Clears the display text (<c>DISPlay:TEXT:CLEar</c>).</summary>
        public void ClearDisplayText() => Send("DISP:TEXT:CLE");

        // ---- status / errors ----------------------------------------------------

        public string NextError() => Query("SYST:ERR?");

        public IReadOnlyList<string> DrainErrors()
        {
            var errors = new List<string>();
            for (int i = 0; i < ErrorQueueDrainCap; i++)
            {
                var e = NextError();
                if (string.IsNullOrEmpty(e) || IsNoError(e)) break;
                errors.Add(e);
            }
            return errors;
        }

        /// <summary>True when a <c>SYSTem:ERRor?</c> reply is the "no error" sentinel (any leading-sign form).</summary>
        private static bool IsNoError(string reply)
        {
            var s = reply.TrimStart('+', ' ');
            return s.StartsWith("0,", StringComparison.Ordinal) || s == "0";
        }

        // ---- parsing ------------------------------------------------------------

        /// <summary>Parses one 34401A reading (e.g. <c>+1.04530000E-03</c>) to a double. The SCPI ±9.9E37
        /// over-range / NaN sentinel is rejected (<see cref="InvalidOperationException"/>), not returned.</summary>
        internal static double ParseReading(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                throw new FormatException("Empty 34401A reading.");
            var s = raw.Trim();
            if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                throw new FormatException($"Unrecognized 34401A reading: '{raw}'.");
            return ScpiReading.Guard(v, s, "34401A");
        }

        /// <summary>Parses a comma-separated 34401A reading list (a <c>READ?</c> / <c>FETCh?</c> burst).</summary>
        internal static double[] ParseReadingList(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                throw new FormatException("Empty 34401A reading list.");
            return raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                      .Select(ParseReading)
                      .ToArray();
        }

        // CONFigure takes either nothing, a range, or "range,resolution". SCPI is positional, so a
        // resolution without a range is expressed as "DEF,<resolution>".
        private static string BuildRangeRes(string range, string resolution)
        {
            var hasRange = !string.IsNullOrWhiteSpace(range);
            var hasRes = !string.IsNullOrWhiteSpace(resolution);
            if (!hasRange && !hasRes) return string.Empty;
            if (hasRange && !hasRes) return $" {range.Trim()}";
            if (!hasRange) return $" DEF,{resolution.Trim()}";
            return $" {range.Trim()},{resolution.Trim()}";
        }

        private static void RequireRangeable(MeasurementFunction function)
        {
            if (!function.IsRangeable())
                throw new ArgumentException($"{function} does not support range/resolution/NPLC settings.", nameof(function));
        }
    }
}
