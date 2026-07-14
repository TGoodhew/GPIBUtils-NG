using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa.Simulation;

namespace GpibUtils.Instruments.Counters
{
    /// <summary>
    /// An in-memory model of an HP 5351A for use with <see cref="SimulatedGpibProvider"/>. Records the
    /// mnemonic commands the driver writes, answers <c>OVEN?</c>/<c>REF?</c> with status strings, and talks
    /// <see cref="Frequency"/> (Hz) on a bare read so the <see cref="Hp5351A"/> driver can be exercised with
    /// no hardware.
    /// </summary>
    public sealed class Hp5351ASimulatedDevice
    {
        public SimulatedInstrument Instrument { get; }

        private readonly List<string> _commands = new List<string>();

        public IReadOnlyList<string> Commands => _commands;

        /// <summary>The frequency (Hz) a read returns.</summary>
        public double Frequency { get; set; }

        /// <summary>Oven status reported by <c>OVEN?</c>.</summary>
        public string Oven { get; set; } = "READY";

        /// <summary>Reference source reported by <c>REF?</c>.</summary>
        public string Reference { get; set; } = "INT";

        public Hp5351ASimulatedDevice()
        {
            Instrument = new SimulatedInstrument
            {
                IdentificationString = "HP,5351A,0,0",
                WriteObserver = cmd => { if (!string.IsNullOrWhiteSpace(cmd)) _commands.Add(cmd.Trim()); },
                Responder = Respond
            };
        }

        private string Respond(string command)
        {
            var upper = (command ?? string.Empty).Trim().ToUpperInvariant();
            if (upper == "OVEN?") return Oven;
            if (upper == "REF?") return Reference;
            // Any other read (bare read after a SAMPLE/INIT command) talks the frequency.
            return Frequency.ToString("+0.00000000E+00;-0.00000000E+00", CultureInfo.InvariantCulture);
        }
    }
}
