using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.SignalSources
{
    /// <summary>
    /// Driver for the HP 8656A/8656B Synthesized Signal Generator — a pre-SCPI, legacy two-letter mnemonic
    /// HP-IB source (issue #122). The instrument is <b>write-only</b> (T0/SR0: it cannot talk, be serial-polled,
    /// or assert SRQ), so there is no <c>*IDN?</c> or readback — <see cref="Identify"/> returns a descriptor and
    /// completion relies on the GPIB 3-wire handshake. Runs over any <see cref="IInstrumentSession"/>.
    /// </summary>
    public sealed class Hp8656 : ISignalSource
    {
        /// <summary>GPIB address of the 8656 — factory default 07 (internal switches). Override with
        /// <c>--address</c>. Never trust bus-scan discovery behind HP-IB extenders.</summary>
        public const string DefaultResource = "GPIB0::7::INSTR";

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public Hp8656(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;
        public IReadOnlyList<string> History => _history;

        private void Send(string command) { _session.Write(command); _history.Add(command); }

        /// <summary>The 8656 cannot talk on the bus (no <c>*IDN?</c>); returns a fixed descriptor.</summary>
        public string Identify() => "HP 8656A/8656B Signal Generator (write-only, no query)";

        /// <summary>Device clear resets to 100 MHz / -127 dBm / no modulation (the documented preset).</summary>
        public void Initialize() => _session.Clear();
        public void Preset() => _session.Clear();

        /// <summary>Sets the carrier frequency (<c>FR &lt;MHz&gt; MZ</c>).</summary>
        public void SetFrequencyMHz(double mhz) =>
            Send("FR" + mhz.ToString("0.######", CultureInfo.InvariantCulture) + "MZ");

        /// <summary>Sets the output amplitude (<c>AP &lt;dBm&gt; DM</c>).</summary>
        public void SetPowerDbm(double dbm) =>
            Send("AP" + dbm.ToString("0.###", CultureInfo.InvariantCulture) + "DM");

        /// <summary>Places the instrument in ON/operate (<c>R1</c>).</summary>
        public void RfOn() => Send("R1");

        /// <summary>Places the instrument in STANDBY (<c>R0</c>).</summary>
        public void RfOff() => Send("R0");
    }
}
