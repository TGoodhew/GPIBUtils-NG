using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using GpibUtils.Visa.Simulation;

namespace GpibUtils.Instruments.SignalSources
{
    /// <summary>
    /// An in-memory model of an HP 3325B for use with <see cref="SimulatedGpibProvider"/>. Decodes the
    /// mnemonics the driver writes (<c>FU{n}</c>, <c>FR</c>, <c>AM</c>, <c>OF</c>, <c>AC</c>) back into
    /// waveform / frequency / amplitude / offset state so the <see cref="Hp3325B"/> driver can be exercised
    /// and asserted with no hardware.
    /// </summary>
    public sealed class Hp3325BSimulatedDevice
    {
        public SimulatedInstrument Instrument { get; }

        private readonly List<string> _commands = new List<string>();

        public IReadOnlyList<string> Commands => _commands;

        /// <summary>Last function code seen (e.g. "FU1"); null after reset.</summary>
        public string Function { get; private set; }

        /// <summary>Last commanded frequency in Hz (converted from the unit suffix); null after reset.</summary>
        public double? FrequencyHz { get; private set; }

        /// <summary>Last commanded amplitude in volts; null after reset.</summary>
        public double? AmplitudeVolts { get; private set; }

        /// <summary>Last commanded DC offset in volts; null after reset.</summary>
        public double? OffsetVolts { get; private set; }

        /// <summary>True once an amplitude calibration (<c>AC</c>) was performed.</summary>
        public bool Calibrated { get; private set; }

        public Hp3325BSimulatedDevice()
        {
            Instrument = new SimulatedInstrument
            {
                IdentificationString = "HEWLETT-PACKARD,3325B,0,0",
                WriteObserver = Apply
            };
        }

        private void Apply(string command)
        {
            var cmd = command.Trim();
            if (cmd.Length == 0) return;
            _commands.Add(cmd);
            var upper = cmd.ToUpperInvariant();

            if (upper == "*RST")
            {
                Function = null; FrequencyHz = null; AmplitudeVolts = null; OffsetVolts = null; Calibrated = false;
                return;
            }
            if (upper == "AC") { Calibrated = true; return; }
            if (Regex.IsMatch(upper, @"^FU\d")) { Function = upper; return; }
            if (upper.StartsWith("FR")) { FrequencyHz = Frequency(cmd, upper); return; }
            if (upper.StartsWith("AM")) { AmplitudeVolts = Volts(cmd, upper); return; }
            if (upper.StartsWith("OF")) { OffsetVolts = Volts(cmd, upper); return; }
        }

        private static double? Number(string s)
        {
            var m = Regex.Match(s, @"[-+]?[0-9]*\.?[0-9]+");
            return m.Success && double.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                ? v : (double?)null;
        }

        private static double? Frequency(string cmd, string upper)
        {
            var n = Number(cmd);
            if (n == null) return null;
            if (upper.Contains("MH")) return n * 1e6;
            if (upper.Contains("KH")) return n * 1e3;
            return n;   // HZ
        }

        private static double? Volts(string cmd, string upper)
        {
            var n = Number(cmd);
            if (n == null) return null;
            return upper.Contains("MV") ? n / 1000.0 : n;   // VO or MV
        }
    }
}
