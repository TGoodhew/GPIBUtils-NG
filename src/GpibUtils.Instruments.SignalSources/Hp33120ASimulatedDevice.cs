using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa.Simulation;

namespace GpibUtils.Instruments.SignalSources
{
    /// <summary>
    /// An in-memory model of an HP 33120A for use with <see cref="SimulatedGpibProvider"/>: decodes the SCPI
    /// setup the <see cref="Hp33120A"/> driver writes (FUNC:SHAP / FREQ / VOLT / VOLT:OFFS) and answers the
    /// matching queries plus <c>*IDN?</c>. No hardware.
    /// </summary>
    public sealed class Hp33120ASimulatedDevice
    {
        public SimulatedInstrument Instrument { get; }

        private readonly List<string> _commands = new List<string>();

        public IReadOnlyList<string> Commands => _commands;
        public string Shape { get; private set; } = "SIN";
        public double FrequencyHz { get; private set; } = 1000;
        public double AmplitudeVpp { get; private set; } = 0.1;
        public double OffsetVolts { get; private set; }

        public Hp33120ASimulatedDevice()
        {
            Instrument = new SimulatedInstrument
            {
                IdentificationString = "HEWLETT-PACKARD,33120A,0,1.0",
                WriteObserver = Apply,
                Responder = Respond
            };
        }

        private void Apply(string command)
        {
            var raw = (command ?? string.Empty).Trim();
            if (raw.Length == 0) return;
            _commands.Add(raw);
            var upper = raw.ToUpperInvariant();
            if (upper == "*RST") { Shape = "SIN"; FrequencyHz = 1000; AmplitudeVpp = 0.1; OffsetVolts = 0; return; }
            if (upper.StartsWith("FUNC:SHAP ")) { Shape = raw.Substring(10).Trim().ToUpperInvariant(); return; }
            if (upper.StartsWith("VOLT:OFFS ")) { OffsetVolts = ParseArg(raw); return; }
            if (upper.StartsWith("VOLT ")) { AmplitudeVpp = ParseArg(raw); return; }
            if (upper.StartsWith("FREQ ")) { FrequencyHz = ParseArg(raw); return; }
        }

        private string Respond(string command)
        {
            var upper = (command ?? string.Empty).Trim().ToUpperInvariant();
            if (upper == "*IDN?") return Instrument.IdentificationString;
            if (upper == "FUNC:SHAP?" || upper == "FUNC?") return Shape;
            if (upper == "FREQ?") return FrequencyHz.ToString("E6", CultureInfo.InvariantCulture);
            if (upper == "VOLT?") return AmplitudeVpp.ToString("E6", CultureInfo.InvariantCulture);
            if (upper == "VOLT:OFFS?") return OffsetVolts.ToString("E6", CultureInfo.InvariantCulture);
            return null;
        }

        private static double ParseArg(string command)
        {
            var idx = command.IndexOf(' ');
            return idx >= 0 && double.TryParse(command.Substring(idx + 1).Trim(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var v) ? v : 0;
        }
    }
}
