using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa.Simulation;

namespace GpibUtils.Instruments.Meters
{
    /// <summary>
    /// An in-memory model of a Rigol DM3058 for use with <see cref="SimulatedGpibProvider"/>. Answers each
    /// one-shot <c>MEASure:…?</c> query with the reading configured for that function, so the
    /// <see cref="RigolDm3058"/> driver can be exercised with no hardware.
    /// </summary>
    public sealed class RigolDm3058SimulatedDevice
    {
        public SimulatedInstrument Instrument { get; }

        private readonly List<string> _commands = new List<string>();
        private readonly Dictionary<MeasurementFunction, double> _readings = new Dictionary<MeasurementFunction, double>();

        public IReadOnlyList<string> Commands => _commands;

        /// <summary>Sets the value a one-shot measurement of <paramref name="function"/> returns.</summary>
        public void SetReading(MeasurementFunction function, double value) => _readings[function] = value;

        public RigolDm3058SimulatedDevice()
        {
            Instrument = new SimulatedInstrument
            {
                IdentificationString = "Rigol Technologies,DM3058,0,01.01",
                WriteObserver = cmd => { if (!string.IsNullOrWhiteSpace(cmd)) _commands.Add(cmd.Trim()); },
                Responder = Respond
            };
        }

        private string Respond(string command)
        {
            var upper = (command ?? string.Empty).Trim().ToUpperInvariant();
            foreach (MeasurementFunction f in Enum.GetValues(typeof(MeasurementFunction)))
                if (upper == RigolDm3058.MeasureQuery(f).ToUpperInvariant())
                    return (_readings.TryGetValue(f, out var v) ? v : 0.0)
                        .ToString("+0.000000E+00;-0.000000E+00", CultureInfo.InvariantCulture);
            if (upper == "SYST:ERR?") return "0,\"No error\"";
            return null;
        }
    }
}
