using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using GpibUtils.Visa.Simulation;

namespace GpibUtils.Instruments.Counters
{
    /// <summary>
    /// An in-memory model of an HP 53131A for use with <see cref="SimulatedGpibProvider"/>, rich enough to
    /// drive the <see cref="Hp53131A"/> driver — including the #43 completion handshake — end to end with no
    /// hardware. It decodes the setup the driver writes (<c>CONF:FREQ (@n)</c>, <c>INP:IMP</c>) and models
    /// the operation-complete → status-byte flow: when a measurement is armed (<c>:INIT</c>) it sets the
    /// Event-Summary bit (0x20) that the completion waiter polls for, then answers <c>FETCH?</c> with
    /// <see cref="Frequency"/>.
    ///
    /// <para>Set <see cref="SignalPresent"/> false to model a missing/too-low signal: the measurement never
    /// completes, so the waiter times out and the driver throws a timeout <see cref="Hp53131AException"/>.</para>
    /// </summary>
    public sealed class Hp53131ASimulatedDevice
    {
        /// <summary>Event-Summary bit (ESB) the driver's completion waiter polls for.</summary>
        private const byte EventSummaryBit = 0x20;

        /// <summary>The <see cref="SimulatedInstrument"/> to register with a <see cref="SimulatedGpibProvider"/>.</summary>
        public SimulatedInstrument Instrument { get; }

        private readonly List<string> _commands = new List<string>();

        /// <summary>Every command the counter was sent (writes and queries), in order (for assertions).</summary>
        public IReadOnlyList<string> Commands => _commands;

        /// <summary>Last channel configured for frequency (from <c>CONF:FREQ (@n)</c>); null before any config.</summary>
        public int? ConfiguredChannel { get; private set; }

        /// <summary>Last input impedance seen: true = 50 Ω (<c>INP:IMP 50</c>), false = 1 MΩ; null if unset.</summary>
        public bool? Is50Ohm { get; private set; }

        /// <summary>The frequency (Hz) a completed measurement returns via <c>FETCH?</c>/<c>READ?</c>.</summary>
        public double Frequency { get; set; }

        /// <summary>When false, an armed measurement never completes (models a missing/low signal), so the
        /// driver's completion waiter times out.</summary>
        public bool SignalPresent { get; set; } = true;

        public Hp53131ASimulatedDevice()
        {
            Instrument = new SimulatedInstrument
            {
                IdentificationString = "HEWLETT-PACKARD,53131A,0,3944",
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

            if (upper.StartsWith("CONF:FREQ"))
            {
                var m = Regex.Match(cmd, @"@\s*(\d+)");
                if (m.Success) ConfiguredChannel = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                return;
            }
            if (upper.StartsWith("INP:IMP"))
            {
                Is50Ohm = upper.Contains("50") && !upper.Contains("1E+6") && !upper.Contains("1E6");
                return;
            }

            // Arming a measurement (the waiter sends "*ESE 1;:INIT;*OPC"). Model the operation-complete
            // flow: a present signal sets the Event-Summary bit the direct-bit waiter polls for; a missing
            // signal leaves the status clear so the wait times out. Set fresh each arm so repeated
            // measurements don't see a stale completion.
            if (upper.Contains(":INIT"))
                Instrument.StatusByte = SignalPresent ? EventSummaryBit : (byte)0;
        }

        private string Respond(string command)
        {
            var upper = (command ?? string.Empty).Trim().ToUpperInvariant();
            if (upper == "FETCH?" || upper == "FETC?" || upper == "READ?")
                return Frequency.ToString("+0.00000000000E+000;-0.00000000000E+000", CultureInfo.InvariantCulture);
            return null;   // fall back to the simulator's common-command handling (*IDN? etc.)
        }
    }
}
