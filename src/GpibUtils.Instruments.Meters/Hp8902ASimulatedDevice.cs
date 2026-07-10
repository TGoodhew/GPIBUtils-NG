using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa.Simulation;

namespace GpibUtils.Instruments.Meters
{
    /// <summary>
    /// An in-memory model of an HP 8902A for use with <see cref="SimulatedGpibProvider"/>, rich enough to
    /// drive the <see cref="Hp8902A"/> driver end to end with no hardware. It decodes the setup mnemonics
    /// the driver writes (mode M4/S4/M5, tuning <c>&lt;f&gt;MZ</c>, offset <c>27.3SP</c>, detector, SET REF,
    /// cal-factor tables) so tests can assert the state the receiver was driven into, and it answers the
    /// settled-read completion handshake: a measurement trigger (<c>T3</c>) sets the Data-Ready status bit,
    /// and the following read returns <see cref="Reading"/> (or <see cref="ReadingOverride"/>).
    ///
    /// <para>Inject faults for the error paths: set <see cref="RecalPending"/> so a read reports RECAL and
    /// the driver throws UNCAL, or <see cref="ReadingOverride"/> to a sentinel / 'CCCC' fill / empty string.</para>
    /// </summary>
    public sealed class Hp8902ASimulatedDevice
    {
        // 8902A status-byte weights the driver's completion handshake polls for.
        private const byte DataReadyBit = 0x01, InstrErrorBit = 0x04, RecalStatusBit = 0x20;

        /// <summary>The <see cref="SimulatedInstrument"/> to register with a <see cref="SimulatedGpibProvider"/>.</summary>
        public SimulatedInstrument Instrument { get; }

        private readonly List<string> _commands = new List<string>();

        /// <summary>Every command the receiver was sent, in order (for assertions).</summary>
        public IReadOnlyList<string> Commands => _commands;

        // ---- decoded state ------------------------------------------------------

        /// <summary>Last measurement mode selected: "M4" (RF Power), "S4" (Tuned RF Level), "M5" (Frequency).</summary>
        public string Mode { get; private set; }

        /// <summary>Last tuned frequency in MHz (from <c>&lt;f&gt;MZ</c>); null before tuning / after preset.</summary>
        public double? TunedMHz { get; private set; }

        /// <summary>True when the receiver is in Frequency-Offset (converter) mode (<c>27.3SP…MZ</c>).</summary>
        public bool OffsetMode { get; private set; }

        /// <summary>Last IF detector special function seen: "4.4SP" (Average) or "4.0SP" (Synchronous).</summary>
        public string Detector { get; private set; }

        /// <summary>True once Track Mode (32.9SP) has been selected.</summary>
        public bool TrackMode { get; private set; }

        /// <summary>True once SET REF (RF) has been taken.</summary>
        public bool ReferenceSet { get; private set; }

        /// <summary>True once the measurement status byte has been unmasked (22.37SP).</summary>
        public bool StatusUnmasked { get; private set; }

        // ---- read behaviour -----------------------------------------------------

        /// <summary>The numeric value a settled read returns (fundamental units: dB, watts, or Hz per mode).
        /// Ignored when <see cref="ReadingOverride"/> is set.</summary>
        public double Reading { get; set; }

        /// <summary>When non-null, the exact string a settled read returns — for sentinel / UNCAL-fill /
        /// empty-read tests. Overrides <see cref="Reading"/>.</summary>
        public string ReadingOverride { get; set; }

        /// <summary>When true, a measurement reports RECAL/UNCAL (status bit 0x20) and produces no result,
        /// so the driver throws <see cref="Hp8902AException"/> (IsUncal).</summary>
        public bool RecalPending { get; set; }

        /// <summary>When true, a measurement asserts the instrument-error status bit (0x04).</summary>
        public bool ErrorPending { get; set; }

        /// <summary>Cal-factor table pair count reported by a 37.4SP query.</summary>
        public int ReportedTablePairs { get; set; }

        /// <summary>Reference cal factor reported by a CF query after 37.5SP.</summary>
        public double ReportedRefCf { get; set; } = 100.0;

        public Hp8902ASimulatedDevice()
        {
            Instrument = new SimulatedInstrument
            {
                IdentificationString = "Hewlett-Packard,8902A,0,0",
                WriteObserver = Apply,
                Responder = Respond
            };
        }

        private void Apply(string command)
        {
            var cmd = command.Trim();
            if (cmd.Length == 0) return;
            _commands.Add(cmd);
            var upper = cmd.ToUpperInvariant();

            if (upper == "IP")
            {
                Mode = null; TunedMHz = null; OffsetMode = false; Detector = null;
                TrackMode = false; ReferenceSet = false; StatusUnmasked = false;
                Instrument.StatusByte = 0;
                return;
            }
            if (upper.StartsWith("M4")) Mode = "M4";       // may be "M4T0" (RF Power + free-run)
            else if (upper == "S4") Mode = "S4";
            else if (upper == "M5") Mode = "M5";

            if (upper == "27.0SP") OffsetMode = false;
            else if (upper.StartsWith("27.3SP")) OffsetMode = true;
            else if (upper == "4.4SP") Detector = "4.4SP";
            else if (upper == "4.0SP") Detector = "4.0SP";
            else if (upper == "32.9SP") TrackMode = true;
            else if (upper == "22.37SP") StatusUnmasked = true;
            else if (upper == "RF") ReferenceSet = true;

            // A tuning command "<freq>MZ" (not the "27.3SP<lo>MZ" offset-entry) sets the tuned frequency.
            if (upper.EndsWith("MZ") && !upper.StartsWith("27.") && !upper.StartsWith("37."))
            {
                var num = ExtractLeadingNumber(cmd);
                if (num.HasValue) TunedMHz = num.Value;
            }

            // A measurement trigger (T3/T0) or a range CALIBRATE (C1) produces a status result: arm the
            // status byte the driver's completion / calibrate-check poll expects.
            if (upper == "T3" || upper == "T0" || upper == "C1")
            {
                byte sb = 0;
                if (ErrorPending) sb |= InstrErrorBit;
                if (RecalPending) sb |= RecalStatusBit;
                if (!RecalPending) sb |= DataReadyBit;   // RECAL-without-result → no Data Ready (driver throws UNCAL)
                Instrument.StatusByte = sb;
            }
        }

        private string Respond(string command)
        {
            var upper = (command ?? string.Empty).Trim().ToUpperInvariant();
            if (upper == "37.4SP") return ReportedTablePairs.ToString(CultureInfo.InvariantCulture);
            if (upper == "CF") return ReportedRefCf.ToString("0.##", CultureInfo.InvariantCulture);
            if (upper == "T3" || upper == "T0")
                return ReadingOverride ?? Reading.ToString("0.######", CultureInfo.InvariantCulture);
            return null;   // fall back to the simulator's common-command handling
        }

        private static double? ExtractLeadingNumber(string s)
        {
            var m = System.Text.RegularExpressions.Regex.Match(s, @"[-+]?[0-9]*\.?[0-9]+");
            return m.Success && double.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                ? v : (double?)null;
        }
    }
}
