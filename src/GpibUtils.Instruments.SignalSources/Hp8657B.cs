using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.SignalSources
{
    /// <summary>
    /// Driver for the HP 8657B Synthesized Signal Generator — a pre-SCPI, legacy two-letter mnemonic HP-IB
    /// source (issue #123). The instrument is <b>listen-only</b> (T0/SR0: no talk, no serial poll, no SRQ), so
    /// there is no <c>*IDN?</c>/readback — <see cref="Identify"/> returns a descriptor, and completion is a
    /// documented settling delay (no readback to confirm). Runs over any <see cref="IInstrumentSession"/>.
    /// </summary>
    public sealed class Hp8657B : ISignalSource
    {
        /// <summary>GPIB address of the 8657B — factory default 07 (internal switch). Override with
        /// <c>--address</c>. Never trust bus-scan discovery behind HP-IB extenders.</summary>
        public const string DefaultResource = "GPIB0::7::INSTR";

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public Hp8657B(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;
        public IReadOnlyList<string> History => _history;

        private void Send(string command) { _session.Write(command); _history.Add(command); }

        /// <summary>The 8657B cannot talk on the bus (no <c>*IDN?</c>); returns a fixed descriptor.</summary>
        public string Identify() => "HP 8657B Signal Generator (listen-only, no query)";

        /// <summary>Device clear = Instrument Preset (100 MHz / -143.5 dBm / no modulation).</summary>
        public void Initialize() => _session.Clear();
        public void Preset() => _session.Clear();

        /// <summary>Sets the carrier frequency (<c>FR &lt;MHz&gt; MZ</c>).</summary>
        public void SetFrequencyMHz(double mhz) =>
            Send("FR" + mhz.ToString("0.######", CultureInfo.InvariantCulture) + "MZ");

        /// <summary>Sets the output amplitude (<c>AP &lt;dBm&gt; DM</c>).</summary>
        public void SetPowerDbm(double dbm) =>
            Send("AP" + dbm.ToString("0.###", CultureInfo.InvariantCulture) + "DM");

        /// <summary>Turns the RF output on (<c>R3</c>).</summary>
        public void RfOn() => Send("R3");

        /// <summary>Turns the RF output off (<c>R2</c>).</summary>
        public void RfOff() => Send("R2");
    }
}
