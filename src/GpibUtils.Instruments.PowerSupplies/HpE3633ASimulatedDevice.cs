using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa.Simulation;

namespace GpibUtils.Instruments.PowerSupplies
{
    /// <summary>
    /// An in-memory model of an HP E3633A for use with <see cref="SimulatedGpibProvider"/>, rich enough to
    /// drive the <see cref="HpE3633A"/> driver end to end with no hardware. It decodes the SCPI the driver
    /// writes (<c>VOLT</c>/<c>CURR</c>/<c>OUTP</c>/<c>VOLT:PROT…</c>) into set-point state, and answers
    /// <c>MEAS:VOLT?</c>/<c>MEAS:CURR?</c>: with the output on it reports the programmed voltage and the
    /// modelled <see cref="LoadCurrent"/>; with the output off it reports zero.
    /// </summary>
    public sealed class HpE3633ASimulatedDevice
    {
        /// <summary>The <see cref="SimulatedInstrument"/> to register with a <see cref="SimulatedGpibProvider"/>.</summary>
        public SimulatedInstrument Instrument { get; }

        private readonly List<string> _commands = new List<string>();

        /// <summary>Every command the supply was sent (writes and queries), in order (for assertions).</summary>
        public IReadOnlyList<string> Commands => _commands;

        /// <summary>Programmed output voltage (V) from the last <c>VOLT</c>.</summary>
        public double Voltage { get; private set; }

        /// <summary>Programmed current limit (A) from the last <c>CURR</c>.</summary>
        public double CurrentLimit { get; private set; }

        /// <summary>Whether the output is enabled (<c>OUTP ON</c>).</summary>
        public bool OutputOn { get; private set; }

        /// <summary>Over-voltage protection trip level (V).</summary>
        public double OverVoltageProtection { get; private set; }

        /// <summary>Whether OVP is enabled.</summary>
        public bool OverVoltageProtectionEnabled { get; private set; }

        /// <summary>The current (A) a <c>MEAS:CURR?</c> reports while the output is on (a simple load model).</summary>
        public double LoadCurrent { get; set; }

        public HpE3633ASimulatedDevice()
        {
            Instrument = new SimulatedInstrument
            {
                IdentificationString = "HEWLETT-PACKARD,E3633A,0,1.0",
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

            if (upper == "*RST")
            {
                Voltage = 0; CurrentLimit = 0; OutputOn = false;
                OverVoltageProtection = 0; OverVoltageProtectionEnabled = false;
                return;
            }
            if (upper == "OUTP ON") { OutputOn = true; return; }
            if (upper == "OUTP OFF") { OutputOn = false; return; }
            if (upper.StartsWith("VOLT:PROT:STAT")) { OverVoltageProtectionEnabled = upper.EndsWith("ON"); return; }
            if (upper.StartsWith("VOLT:PROT")) { OverVoltageProtection = Number(cmd) ?? OverVoltageProtection; return; }
            if (upper.StartsWith("VOLT")) { Voltage = Number(cmd) ?? Voltage; return; }
            if (upper.StartsWith("CURR")) { CurrentLimit = Number(cmd) ?? CurrentLimit; return; }
        }

        private string Respond(string command)
        {
            var upper = (command ?? string.Empty).Trim().ToUpperInvariant();
            if (upper == "MEAS:VOLT?") return Fmt(OutputOn ? Voltage : 0.0);
            if (upper == "MEAS:CURR?") return Fmt(OutputOn ? LoadCurrent : 0.0);
            if (upper == "SYST:ERR?") return "+0,\"No error\"";
            return null;   // fall back to common-command handling (*IDN? etc.)
        }

        private static string Fmt(double v) => v.ToString("+0.000000E+00;-0.000000E+00", CultureInfo.InvariantCulture);

        private static double? Number(string s)
        {
            var m = System.Text.RegularExpressions.Regex.Match(s, @"[-+]?[0-9]*\.?[0-9]+([eE][-+]?[0-9]+)?");
            return m.Success && double.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                ? v : (double?)null;
        }
    }
}
