using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using GpibUtils.Visa.Simulation;

namespace GpibUtils.Instruments.Meters
{
    /// <summary>
    /// An in-memory model of an HP 34401A for use with <see cref="SimulatedGpibProvider"/>, rich enough to
    /// drive the <see cref="Hp34401A"/> driver end to end with no hardware. It decodes the SCPI the driver
    /// writes (CONFigure / SENSe / TRIGger / SAMPle / CALCulate / DISPlay) so tests can assert the state
    /// the DMM was driven into, and it answers the reads: <c>READ?</c> / <c>FETCh?</c> return a burst sized
    /// by the sample count (each value <see cref="Reading"/>, or the explicit <see cref="Readings"/> list),
    /// and the query surface (<c>*IDN?</c> / <c>CONF?</c> / <c>FUNC?</c> / <c>SYST:ERR?</c> / <c>*TST?</c> /
    /// <c>CALC:AVER:*</c>) is modelled.
    ///
    /// <para>Inject faults for the error paths: queue entries in <see cref="Errors"/> so
    /// <see cref="Hp34401A.DrainErrors"/> reads them, set <see cref="ReadingOverride"/> to a malformed
    /// string, or set <see cref="SelfTestPasses"/> false.</para>
    /// </summary>
    public sealed class Hp34401ASimulatedDevice
    {
        /// <summary>The <see cref="SimulatedInstrument"/> to register with a <see cref="SimulatedGpibProvider"/>.</summary>
        public SimulatedInstrument Instrument { get; }

        private readonly List<string> _commands = new List<string>();
        private readonly Queue<string> _errorQueue = new Queue<string>();

        /// <summary>Every command the DMM was sent (writes and queries), in order (for assertions).</summary>
        public IReadOnlyList<string> Commands => _commands;

        // ---- decoded state ------------------------------------------------------

        /// <summary>SCPI function root last selected via CONFigure (e.g. "VOLT:DC", "RES", "CONT"); null after preset.</summary>
        public string Function { get; private set; }

        /// <summary>Raw range/resolution argument last given to CONFigure (trimmed), or null when omitted.</summary>
        public string ConfiguredRange { get; private set; }

        /// <summary>Last NPLC set for any function (via <c>&lt;func&gt;:NPLC</c>); null if never set.</summary>
        public double? Nplc { get; private set; }

        /// <summary>Last trigger source keyword seen ("IMM" / "BUS" / "EXT"); null after preset.</summary>
        public string TriggerSource { get; private set; }

        /// <summary>Last trigger count (TRIG:COUN); defaults to 1.</summary>
        public int TriggerCount { get; private set; } = 1;

        /// <summary>Last sample count (SAMP:COUN); defaults to 1 and sizes a READ?/FETCh? burst.</summary>
        public int SampleCount { get; private set; } = 1;

        /// <summary>True when DC input impedance is auto (INP:IMP:AUTO ON).</summary>
        public bool InputImpedanceAuto { get; private set; }

        /// <summary>Last autozero keyword seen ("OFF"/"ONCE"/"ON"); null if never set.</summary>
        public string AutoZero { get; private set; }

        /// <summary>Front-panel display enabled (DISP ON); true at preset.</summary>
        public bool DisplayOn { get; private set; } = true;

        /// <summary>Last text pushed to the display (DISP:TEXT), or null after a clear/preset.</summary>
        public string DisplayText { get; private set; }

        /// <summary>Last CALC:FUNC keyword ("NULL"/"DB"/"DBM"/"AVER"/"LIM"); null if never set.</summary>
        public string MathFunction { get; private set; }

        /// <summary>Whether math is enabled (CALC:STAT ON).</summary>
        public bool MathEnabled { get; private set; }

        /// <summary>True once *RST has been seen (cleared by the next CONFigure).</summary>
        public bool WasReset { get; private set; }

        /// <summary>Which input terminals a ROUT:TERM? query reports ("FRON" or "REAR").</summary>
        public string Terminals { get; set; } = "FRON";

        // ---- read behaviour -----------------------------------------------------

        /// <summary>The value each sample of a READ?/FETCh? burst returns; ignored when
        /// <see cref="Readings"/> or <see cref="ReadingOverride"/> is set.</summary>
        public double Reading { get; set; }

        /// <summary>Explicit burst values a READ?/FETCh? returns (comma-joined). Overrides <see cref="Reading"/>
        /// / <see cref="SampleCount"/> sizing when set.</summary>
        public IReadOnlyList<double> Readings { get; set; }

        /// <summary>When non-null, the exact string READ?/FETCh? returns — for malformed-read tests.</summary>
        public string ReadingOverride { get; set; }

        /// <summary>Statistics a CALC:AVER:MIN?/MAX?/AVER?/COUN? query reports.</summary>
        public double AverageMin { get; set; }
        public double AverageMax { get; set; }
        public double AverageMean { get; set; }
        public int AverageCount { get; set; }

        /// <summary>Whether *TST? reports a pass (+0).</summary>
        public bool SelfTestPasses { get; set; } = true;

        /// <summary>Queues an error string to be returned by successive SYSTem:ERRor? queries.</summary>
        public void QueueError(string error) => _errorQueue.Enqueue(error);

        public Hp34401ASimulatedDevice()
        {
            Instrument = new SimulatedInstrument
            {
                IdentificationString = "HEWLETT-PACKARD,34401A,0,11-5-2",
                WriteObserver = Apply,
                Responder = Respond
            };
        }

        private void Apply(string command)
        {
            var cmd = command.Trim();
            if (cmd.Length == 0) return;
            _commands.Add(cmd);

            // Queries carry no state change here (they are answered in Respond); decode only writes.
            if (cmd.EndsWith("?", StringComparison.Ordinal)) return;

            var upper = cmd.ToUpperInvariant();

            if (upper == "*RST")
            {
                Function = null; ConfiguredRange = null; Nplc = null; TriggerSource = null;
                TriggerCount = 1; SampleCount = 1; InputImpedanceAuto = false; AutoZero = null;
                DisplayOn = true; DisplayText = null; MathFunction = null; MathEnabled = false;
                WasReset = true;
                return;
            }
            if (upper == "*CLS") { _errorQueue.Clear(); return; }

            if (upper.StartsWith("CONF:"))
            {
                DecodeConfigure(cmd.Substring("CONF:".Length));
                WasReset = false;
                return;
            }
            if (TryDecodeNplc(upper)) return;
            if (upper.StartsWith("TRIG:SOUR")) { TriggerSource = LastToken(upper); return; }
            if (upper.StartsWith("TRIG:COUN")) { TriggerCount = (int)(ExtractNumber(cmd) ?? TriggerCount); return; }
            if (upper.StartsWith("SAMP:COUN")) { SampleCount = (int)(ExtractNumber(cmd) ?? SampleCount); return; }
            if (upper.StartsWith("INP:IMP:AUTO")) { InputImpedanceAuto = LastToken(upper) == "ON"; return; }
            if (upper.StartsWith("ZERO:AUTO")) { AutoZero = LastToken(upper); return; }
            if (upper.StartsWith("CALC:FUNC")) { MathFunction = LastToken(upper); return; }
            if (upper.StartsWith("CALC:STAT")) { MathEnabled = LastToken(upper) == "ON"; return; }
            if (upper == "DISP ON") { DisplayOn = true; return; }
            if (upper == "DISP OFF") { DisplayOn = false; return; }
            if (upper == "DISP:TEXT:CLE") { DisplayText = null; return; }
            if (upper.StartsWith("DISP:TEXT"))
            {
                var m = Regex.Match(cmd, "\"(.*)\"");
                if (m.Success) DisplayText = m.Groups[1].Value;
                return;
            }
        }

        private void DecodeConfigure(string tail)
        {
            // tail is e.g. "VOLT:DC 10,0.001" or "RES" or "CONT". The function root is the leading
            // colon-joined keyword run; anything after the first space is the range[/resolution] argument.
            var trimmed = tail.Trim();
            int sp = trimmed.IndexOf(' ');
            if (sp < 0)
            {
                Function = trimmed.ToUpperInvariant();
                ConfiguredRange = null;
            }
            else
            {
                Function = trimmed.Substring(0, sp).ToUpperInvariant();
                ConfiguredRange = trimmed.Substring(sp + 1).Trim();
            }
        }

        private bool TryDecodeNplc(string upper)
        {
            // "<func>:NPLC <value>" — capture the value regardless of which function it targets.
            if (!upper.Contains(":NPLC")) return false;
            Nplc = ExtractNumber(upper);
            return true;
        }

        private string Respond(string command)
        {
            var raw = (command ?? string.Empty).Trim();
            var upper = raw.ToUpperInvariant();

            if (upper == "READ?" || upper == "FETC?" || upper == "FETCH?") return ReadResponse();
            if (upper == "CONF?") return "\"" + (Function ?? "VOLT +1.00000000E+01,+3.00000000E-06") + "\"";
            if (upper == "FUNC?") return "\"" + (Function ?? "VOLT") + "\"";
            if (upper == "ROUT:TERM?") return Terminals;
            if (upper == "*TST?") return SelfTestPasses ? "+0" : "+1";
            if (upper == "SYST:ERR?") return _errorQueue.Count > 0 ? _errorQueue.Dequeue() : Hp34401A.NoError;
            if (upper == "CALC:AVER:MIN?") return Fmt(AverageMin);
            if (upper == "CALC:AVER:MAX?") return Fmt(AverageMax);
            if (upper == "CALC:AVER:AVER?") return Fmt(AverageMean);
            if (upper == "CALC:AVER:COUN?") return AverageCount.ToString(CultureInfo.InvariantCulture);

            return null;   // fall back to the simulator's common-command handling
        }

        private string ReadResponse()
        {
            if (ReadingOverride != null) return ReadingOverride;
            IEnumerable<double> values = Readings ?? Enumerable.Repeat(Reading, Math.Max(1, SampleCount));
            return string.Join(",", values.Select(Fmt));
        }

        private static string Fmt(double v) => v.ToString("+0.00000000E+00;-0.00000000E+00", CultureInfo.InvariantCulture);

        private static string LastToken(string upper)
        {
            var parts = upper.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[parts.Length - 1] : string.Empty;
        }

        private static double? ExtractNumber(string s)
        {
            var m = Regex.Match(s, @"[-+]?[0-9]*\.?[0-9]+([eE][-+]?[0-9]+)?");
            return m.Success && double.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                ? v : (double?)null;
        }
    }
}
