using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.Switches
{
    /// <summary>One plug-in slot of an HP 3499A and the card type it reports.</summary>
    public struct SlotCard
    {
        public int Slot { get; }

        /// <summary>The <c>SYSTem:CTYPE?</c> reply for the slot (e.g. a "…,44472A,…" identity, or an
        /// "empty"/"0" style response when the slot is unpopulated).</summary>
        public string CardType { get; }

        public SlotCard(int slot, string cardType)
        {
            Slot = slot;
            CardType = cardType;
        }

        public override string ToString() => $"slot {Slot}: {CardType}";
    }

    /// <summary>
    /// Driver for the HP 3499A Switch/Control System — a plain SCPI mainframe that carries plug-in relay /
    /// switch cards (e.g. the 44472A dual 4-channel VHF switch and 44476B microwave switch) and digital-I/O
    /// modules. Channels are addressed as <c>snn</c> (slot <c>s</c> + two-digit channel <c>nn</c>, e.g. 100 =
    /// slot 1 channel 00; a matrix card uses slot-row-column). Opens/closes relay channels and enumerates
    /// installed cards, over any <see cref="IInstrumentSession"/>. Ported from <c>HP3499Demo</c> (issue #4);
    /// the 44472A/44476B are plug-ins addressed via this mainframe scheme, not separate instruments.
    /// </summary>
    public sealed class Hp3499A
    {
        /// <summary>GPIB address of the 3499A — its documented factory-default GPIB address is 9 (3499A
        /// User's &amp; Programming Guide: "When shipped from the factory, the GPIB interface is selected and its
        /// address is set to '9'"); the <c>HP3499Demo</c> source uses the same. Override with <c>--address</c>.
        /// Never trust bus-scan discovery here — the HP-IB extenders make every address look present.</summary>
        public const string DefaultResource = "GPIB0::9::INSTR";

        /// <summary>Number of slots enumerated by <see cref="ListCards"/> by default: slot 0 (the built-in
        /// controller) through slot 5, matching the 3499A mainframe and the legacy demo's 0–5 scan.</summary>
        public const int DefaultSlotCount = 6;

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public Hp3499A(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;

        /// <summary>Every command sent through the driver, in order (for CLI echo / tests).</summary>
        public IReadOnlyList<string> History => _history;

        private void Send(string command)
        {
            _session.Write(command);
            _history.Add(command);
        }

        private string Query(string command)
        {
            _history.Add(command);
            return (_session.Query(command) ?? string.Empty).Trim();
        }

        public string Identify() => Query("*IDN?");

        public void Initialize()
        {
            _session.Clear();          // GPIB device clear
            Send("*RST");              // reset the mainframe (all relays to their reset state)
            Send("*CLS");              // clear status
            Send("*SRE 0");
            Send("*ESE 0");
            Send(":STAT:PRES");        // preset the SCPI status subsystem
        }

        public void Reset() => Send("*RST");

        /// <summary>Composes an <c>snn</c> channel address from a slot and a channel number (0–99).</summary>
        public static int ChannelAddress(int slot, int channel)
        {
            if (slot < 0) throw new ArgumentOutOfRangeException(nameof(slot), slot, "Slot must be >= 0.");
            if (channel < 0 || channel > 99)
                throw new ArgumentOutOfRangeException(nameof(channel), channel, "Channel must be 0–99.");
            return slot * 100 + channel;
        }

        /// <summary>Closes a relay channel (<c>ROUTe:CLOSe (@snn)</c>).</summary>
        public void Close(int channel) => Send("ROUT:CLOS (@" + Ch(channel) + ")");

        /// <summary>Opens a relay channel (<c>ROUTe:OPEN (@snn)</c>).</summary>
        public void Open(int channel) => Send("ROUT:OPEN (@" + Ch(channel) + ")");

        /// <summary>True if the relay channel is closed (<c>ROUTe:CLOSe? (@snn)</c> returns 1).</summary>
        public bool IsClosed(int channel)
        {
            var reply = Query("ROUT:CLOS? (@" + Ch(channel) + ")");
            return ParseBooleanReply(reply);
        }

        /// <summary>Reads the plug-in card type reported for a slot (<c>SYSTem:CTYPE? &lt;slot&gt;</c>).</summary>
        public string GetCardType(int slot)
        {
            if (slot < 0) throw new ArgumentOutOfRangeException(nameof(slot), slot, "Slot must be >= 0.");
            return Query("SYST:CTYPE? " + slot.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>Enumerates the card type in each slot from 0 to <paramref name="slotCount"/>-1.</summary>
        public IReadOnlyList<SlotCard> ListCards(int slotCount = DefaultSlotCount)
        {
            if (slotCount < 1) throw new ArgumentOutOfRangeException(nameof(slotCount), slotCount, "Slot count must be >= 1.");
            var cards = new List<SlotCard>(slotCount);
            for (int slot = 0; slot < slotCount; slot++)
                cards.Add(new SlotCard(slot, GetCardType(slot)));
            return cards;
        }

        private static string Ch(int channel)
        {
            if (channel < 0) throw new ArgumentOutOfRangeException(nameof(channel), channel, "Channel address must be >= 0.");
            return channel.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Interprets a 3499A boolean reply (<c>1</c>/<c>0</c>, or <c>ON</c>/<c>OFF</c>) as closed/open.</summary>
        internal static bool ParseBooleanReply(string reply)
        {
            var s = (reply ?? string.Empty).Trim();
            if (s.Length == 0) throw new FormatException("Empty 3499A boolean reply.");
            if (s == "1" || s.Equals("ON", StringComparison.OrdinalIgnoreCase)) return true;
            if (s == "0" || s.Equals("OFF", StringComparison.OrdinalIgnoreCase)) return false;
            // Tolerate a leading-sign / decimal form (e.g. "+1").
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return v != 0;
            throw new FormatException($"Unrecognized 3499A boolean reply: '{reply}'.");
        }
    }
}
