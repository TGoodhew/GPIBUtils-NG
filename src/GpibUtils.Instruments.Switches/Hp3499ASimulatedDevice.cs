using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using GpibUtils.Visa.Simulation;

namespace GpibUtils.Instruments.Switches
{
    /// <summary>
    /// An in-memory model of an HP 3499A for use with <see cref="SimulatedGpibProvider"/>, rich enough to
    /// drive the <see cref="Hp3499A"/> driver end to end with no hardware. It tracks relay channel state
    /// (<c>ROUTe:CLOSe</c>/<c>OPEN (@snn)</c>), answers <c>ROUTe:CLOSe? (@snn)</c> with the channel's
    /// open/closed state, and answers <c>SYSTem:CTYPE? &lt;slot&gt;</c> from a configurable slot→card map so
    /// card enumeration can be exercised and asserted.
    /// </summary>
    public sealed class Hp3499ASimulatedDevice
    {
        /// <summary>The <see cref="SimulatedInstrument"/> to register with a <see cref="SimulatedGpibProvider"/>.</summary>
        public SimulatedInstrument Instrument { get; }

        private readonly List<string> _commands = new List<string>();
        private readonly HashSet<int> _closed = new HashSet<int>();
        private readonly Dictionary<int, string> _cards = new Dictionary<int, string>();

        /// <summary>Every command the mainframe was sent (writes and queries), in order (for assertions).</summary>
        public IReadOnlyList<string> Commands => _commands;

        /// <summary>The set of currently-closed channel addresses (snn).</summary>
        public IReadOnlyCollection<int> ClosedChannels => _closed;

        /// <summary>The <c>SYSTem:CTYPE?</c> reply for an unpopulated slot (an empty/zeroed identity).</summary>
        public string EmptySlotResponse { get; set; } = "0,0,0,0";

        public Hp3499ASimulatedDevice()
        {
            Instrument = new SimulatedInstrument
            {
                IdentificationString = "HEWLETT-PACKARD,3499A,0,1.0",
                WriteObserver = Apply,
                Responder = Respond
            };
        }

        /// <summary>Populates a slot with a card type reported by <c>SYSTem:CTYPE?</c>.</summary>
        public Hp3499ASimulatedDevice WithCard(int slot, string cardType)
        {
            _cards[slot] = cardType;
            return this;
        }

        /// <summary>True if the given channel address (snn) is currently closed.</summary>
        public bool IsClosed(int channel) => _closed.Contains(channel);

        private void Apply(string command)
        {
            var cmd = command.Trim();
            if (cmd.Length == 0) return;
            _commands.Add(cmd);
            var upper = cmd.ToUpperInvariant();

            if (upper == "*RST") { _closed.Clear(); return; }   // reset opens all relays

            // A CLOSe / OPEN write (not the "?" query) toggles the channel's state.
            if (upper.StartsWith("ROUT:CLOS") && !upper.Contains("?"))
            {
                var ch = ExtractChannel(cmd);
                if (ch.HasValue) _closed.Add(ch.Value);
                return;
            }
            if (upper.StartsWith("ROUT:OPEN"))
            {
                var ch = ExtractChannel(cmd);
                if (ch.HasValue) _closed.Remove(ch.Value);
                return;
            }
        }

        private string Respond(string command)
        {
            var raw = (command ?? string.Empty).Trim();
            var upper = raw.ToUpperInvariant();

            if (upper.StartsWith("ROUT:CLOS?"))
            {
                var ch = ExtractChannel(raw);
                return ch.HasValue && _closed.Contains(ch.Value) ? "1" : "0";
            }
            if (upper.StartsWith("SYST:CTYPE?"))
            {
                var slot = ExtractSlot(raw);
                if (slot.HasValue && _cards.TryGetValue(slot.Value, out var card)) return card;
                return EmptySlotResponse;
            }
            return null;   // fall back to the simulator's common-command handling (*IDN? etc.)
        }

        // Pulls the channel number out of a "(@snn)" channel list (the first number after '@').
        private static int? ExtractChannel(string s)
        {
            var m = Regex.Match(s, @"@\s*(\d+)");
            return m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : (int?)null;
        }

        // Pulls the slot number out of "SYST:CTYPE? <slot>".
        private static int? ExtractSlot(string s)
        {
            var m = Regex.Match(s, @"CTYPE\?\s*(\d+)", RegexOptions.IgnoreCase);
            return m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : (int?)null;
        }
    }
}
