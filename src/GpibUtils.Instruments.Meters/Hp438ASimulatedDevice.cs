using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa.Simulation;

namespace GpibUtils.Instruments.Meters
{
    /// <summary>
    /// An in-memory model of an HP 438A power meter for use with <see cref="SimulatedGpibProvider"/>. It
    /// records the mnemonic commands the driver writes and answers a channel read (<c>{A|B}P TR2</c>) with
    /// that channel's power (dBm), so the <see cref="Hp438A"/> driver can be exercised with no hardware.
    /// </summary>
    public sealed class Hp438ASimulatedDevice
    {
        public SimulatedInstrument Instrument { get; }

        private readonly List<string> _commands = new List<string>();

        public IReadOnlyList<string> Commands => _commands;

        /// <summary>Power (dBm) a channel-A read returns.</summary>
        public double PowerDbmA { get; set; }

        /// <summary>Power (dBm) a channel-B read returns.</summary>
        public double PowerDbmB { get; set; }

        /// <summary>When true, a read returns the 438A over-range error value instead of a number.</summary>
        public bool OverRange { get; set; }

        /// <summary>True once <c>ZE</c> (zero) was sent.</summary>
        public bool Zeroed { get; private set; }

        public Hp438ASimulatedDevice()
        {
            Instrument = new SimulatedInstrument
            {
                IdentificationString = "HP,438A,0,0",
                WriteObserver = Apply,
                Responder = Respond
            };
        }

        private void Apply(string command)
        {
            var cmd = command.Trim();
            if (cmd.Length == 0) return;
            _commands.Add(cmd);
            if (cmd.ToUpperInvariant() == "ZE") Zeroed = true;
        }

        private string Respond(string command)
        {
            var upper = (command ?? string.Empty).Trim().ToUpperInvariant();
            if (upper.StartsWith("AP") || upper.StartsWith("BP"))
            {
                if (OverRange) return "9.10000000E+40";
                double p = upper.StartsWith("A") ? PowerDbmA : PowerDbmB;
                return p.ToString("+0.00000000E+00;-0.00000000E+00", CultureInfo.InvariantCulture);
            }
            return null;
        }
    }
}
