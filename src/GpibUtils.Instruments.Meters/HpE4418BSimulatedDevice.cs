using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using GpibUtils.Visa.Simulation;

namespace GpibUtils.Instruments.Meters
{
    /// <summary>
    /// An in-memory model of an HP E4418B power meter for use with <see cref="SimulatedGpibProvider"/>,
    /// rich enough to drive the <see cref="HpE4418B"/> driver — including its #43 OPC→SRQ completion — with
    /// no hardware. Decodes <c>:FREQ …MHZ</c>, and on an armed operation (<c>*OPC</c> in a compound write)
    /// sets the Event-Summary bit the completion waiter polls for; <c>FETCH?</c> returns <see cref="PowerDbm"/>.
    /// </summary>
    public sealed class HpE4418BSimulatedDevice
    {
        private const byte EventSummaryBit = 0x20;

        public SimulatedInstrument Instrument { get; }

        private readonly List<string> _commands = new List<string>();

        public IReadOnlyList<string> Commands => _commands;

        /// <summary>Last cal-factor carrier frequency set (MHz), from <c>:FREQ …MHZ</c>; null if unset.</summary>
        public double? FrequencyMHz { get; private set; }

        /// <summary>The power (dBm) a <c>FETCH?</c> reports.</summary>
        public double PowerDbm { get; set; }

        public HpE4418BSimulatedDevice()
        {
            Instrument = new SimulatedInstrument
            {
                IdentificationString = "Hewlett-Packard,E4418B,0,A1.00.00",
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

            if (upper == "*RST" || upper == "*CLS") { Instrument.StatusByte = 0; return; }
            if (upper.StartsWith(":FREQ"))
            {
                var m = Regex.Match(cmd, @"([-+]?[0-9]*\.?[0-9]+)");
                if (m.Success) FrequencyMHz = double.Parse(m.Value, CultureInfo.InvariantCulture);
                return;
            }
            // Any OPC-armed operation completes: raise the Event-Summary bit the direct-bit waiter polls for.
            if (upper.Contains("*OPC")) Instrument.StatusByte = EventSummaryBit;
        }

        private string Respond(string command)
        {
            var upper = (command ?? string.Empty).Trim().ToUpperInvariant();
            if (upper == "FETCH?" || upper == "FETC?")
                return PowerDbm.ToString("+0.00000000E+00;-0.00000000E+00", CultureInfo.InvariantCulture);
            return null;
        }
    }
}
