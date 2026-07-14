using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa.Simulation;

namespace GpibUtils.Instruments.Meters
{
    /// <summary>
    /// An in-memory model of an HP 3458A for use with <see cref="SimulatedGpibProvider"/>. Decodes the
    /// native setup commands the <see cref="Hp3458A"/> driver writes (<c>FUNC</c>, <c>NPLC</c>, <c>RES</c>,
    /// <c>SETACV</c>) and answers a triggered read (<c>TARM SGL</c>) with <see cref="Reading"/> and
    /// <c>ID?</c> with the model.
    /// </summary>
    public sealed class Hp3458ASimulatedDevice
    {
        public SimulatedInstrument Instrument { get; }

        private readonly List<string> _commands = new List<string>();

        public IReadOnlyList<string> Commands => _commands;

        /// <summary>Last function keyword selected via <c>FUNC …</c> (e.g. "DCV", "ACV"); null before config.</summary>
        public string Function { get; private set; }

        /// <summary>True once <c>SETACV SYNC</c> was sent.</summary>
        public bool AcSync { get; private set; }

        /// <summary>Last NPLC set; null if never set.</summary>
        public double? Nplc { get; private set; }

        /// <summary>The value a triggered read (<c>TARM SGL</c>) returns.</summary>
        public double Reading { get; set; }

        public Hp3458ASimulatedDevice()
        {
            Instrument = new SimulatedInstrument
            {
                IdentificationString = "HP3458A",
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

            if (upper == "RESET") { Function = null; AcSync = false; Nplc = null; return; }
            if (upper.StartsWith("FUNC ")) { Function = upper.Substring(5).Trim(); return; }
            if (upper.StartsWith("SETACV")) { AcSync = upper.Contains("SYNC"); return; }
            if (upper.StartsWith("NPLC"))
            {
                if (double.TryParse(upper.Substring(4).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var n)) Nplc = n;
                return;
            }
        }

        private string Respond(string command)
        {
            var upper = (command ?? string.Empty).Trim().ToUpperInvariant();
            if (upper == "ID?") return "HP3458A";
            if (upper == "TARM SGL")
                return Reading.ToString("+0.00000000E+00;-0.00000000E+00", CultureInfo.InvariantCulture);
            return null;
        }
    }
}
