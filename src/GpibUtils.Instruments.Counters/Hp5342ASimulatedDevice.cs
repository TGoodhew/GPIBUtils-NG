using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa.Simulation;

namespace GpibUtils.Instruments.Counters
{
    /// <summary>
    /// An in-memory model of an HP 5342A for use with <see cref="SimulatedGpibProvider"/>. Records the
    /// mnemonic commands the driver writes and talks <see cref="Frequency"/> (Hz) on a read — or the 5342A
    /// over/under-level dashes when <see cref="OverLevel"/> is set — so the <see cref="Hp5342A"/> driver can
    /// be exercised with no hardware.
    /// </summary>
    public sealed class Hp5342ASimulatedDevice
    {
        public SimulatedInstrument Instrument { get; }

        private readonly List<string> _commands = new List<string>();

        public IReadOnlyList<string> Commands => _commands;

        /// <summary>The frequency (Hz) a read returns.</summary>
        public double Frequency { get; set; }

        /// <summary>When true, a read returns the 5342A over/under-level dashes instead of a number.</summary>
        public bool OverLevel { get; set; }

        public Hp5342ASimulatedDevice()
        {
            Instrument = new SimulatedInstrument
            {
                IdentificationString = "HP,5342A,0,0",
                WriteObserver = cmd => { if (!string.IsNullOrWhiteSpace(cmd)) _commands.Add(cmd.Trim()); },
                Responder = _ => OverLevel
                    ? "----------"
                    : Frequency.ToString("+0.00000000E+00;-0.00000000E+00", CultureInfo.InvariantCulture)
            };
        }
    }
}
