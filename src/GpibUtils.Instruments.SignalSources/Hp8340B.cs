using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.SignalSources
{
    /// <summary>
    /// Driver for the HP 8340B Synthesized Sweeper used as a CW signal source. HP-IB codes
    /// (8340B Operating manual, Table 3-2): <c>IP</c> (instrument preset), <c>CW &lt;val&gt; MZ</c>
    /// (frequency in MHz), <c>PL &lt;val&gt; DB</c> (power; the DB terminator is dB(m)), <c>RF1</c>/<c>RF0</c>.
    /// The 8340B is driven write-only here; it runs over any <see cref="IInstrumentSession"/>.
    /// </summary>
    public sealed class Hp8340B : ISignalSource
    {
        /// <summary>GPIB address of the 8340B on the reference bench (the migrated harness default).</summary>
        public const string DefaultResource = "GPIB0::20::INSTR";

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public Hp8340B(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;

        /// <summary>Every HP-IB mnemonic sent, in order.</summary>
        public IReadOnlyList<string> History => _history;

        private void Send(string command)
        {
            _session.Write(command);
            _history.Add(command);
        }

        public void Initialize()
        {
            _session.Clear();   // GPIB device clear — drop any pending I/O / SRQ
            Send("IP");         // instrument preset
            Send("RF0");        // RF off until we set up
        }

        public void Preset() => Send("IP");

        public void SetFrequencyMHz(double mhz) =>
            Send("CW " + mhz.ToString("0.######", CultureInfo.InvariantCulture) + " MZ");

        public void SetPowerDbm(double dbm) =>
            Send("PL " + dbm.ToString("0.###", CultureInfo.InvariantCulture) + " DB");

        public void RfOn() => Send("RF1");

        public void RfOff() => Send("RF0");
    }
}
