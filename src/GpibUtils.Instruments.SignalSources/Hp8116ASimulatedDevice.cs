using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using GpibUtils.Visa.Simulation;

namespace GpibUtils.Instruments.SignalSources
{
    /// <summary>
    /// An in-memory model of an HP 8116A for use with <see cref="SimulatedGpibProvider"/>: decodes the legacy
    /// mnemonic setup the <see cref="Hp8116A"/> driver writes (FRQ/AMP/OFS/DTY, D0/D1) and models the
    /// serial-poll status byte + <c>IERR</c> error read. No hardware.
    /// </summary>
    public sealed class Hp8116ASimulatedDevice
    {
        public SimulatedInstrument Instrument { get; }

        private readonly List<string> _commands = new List<string>();

        public IReadOnlyList<string> Commands => _commands;
        public double FrequencyHz { get; private set; } = 1000;
        public double AmplitudeVpp { get; private set; } = 1;
        public double OffsetVolts { get; private set; }
        public double DutyPercent { get; private set; } = 50;
        public bool OutputEnabled { get; private set; }
        public string ErrorText { get; set; } = "";

        public Hp8116ASimulatedDevice()
        {
            Instrument = new SimulatedInstrument
            {
                IdentificationString = "HP8116A",
                WriteObserver = Apply,
                Responder = Respond
            };
        }

        /// <summary>Injects a fault: sets the given status-byte bit(s) + the SRQ summary and an IERR string.</summary>
        public void InjectFault(int statusBits, string errorText)
        {
            Instrument.StatusByte = (byte)statusBits;
            Instrument.ServiceRequestPending = true;
            ErrorText = errorText;
        }

        private void Apply(string command)
        {
            var raw = (command ?? string.Empty).Trim();
            if (raw.Length == 0) return;
            var upper = raw.ToUpperInvariant();
            _commands.Add(upper);
            if (upper == "D0") { OutputEnabled = true; return; }
            if (upper == "D1") { OutputEnabled = false; return; }
            var m = Regex.Match(upper, @"^(FRQ|AMP|OFS|DTY)\s+([-+0-9.eE]+)");
            if (!m.Success) return;
            double v = double.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;
            switch (m.Groups[1].Value)
            {
                case "FRQ": FrequencyHz = v; break;
                case "AMP": AmplitudeVpp = v; break;
                case "OFS": OffsetVolts = v; break;
                case "DTY": DutyPercent = v; break;
            }
        }

        private string Respond(string command)
        {
            var upper = (command ?? string.Empty).Trim().ToUpperInvariant();
            if (upper == "IERR") return ErrorText;
            return null;
        }
    }
}
