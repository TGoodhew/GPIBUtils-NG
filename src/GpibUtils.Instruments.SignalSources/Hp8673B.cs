using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.SignalSources
{
    /// <summary>
    /// Driver for the HP 8673B Synthesized Signal Generator (2–26.5 GHz), used as the external LO for the
    /// 11793A converter path. HP-IB codes (8673B Operating manual): <c>IP</c> (instrument preset),
    /// <c>FR &lt;val&gt; MZ</c> (frequency in MHz), <c>LE &lt;val&gt; DM</c> (level in dBm), <c>RF1</c>/<c>RF0</c>.
    /// Driven write-only here; runs over any <see cref="IInstrumentSession"/>.
    /// </summary>
    public sealed class Hp8673B : ILocalOscillator
    {
        /// <summary>GPIB address of the 8673B on the reference bench (the migrated harness LO default).</summary>
        public const string DefaultResource = "GPIB0::19::INSTR";

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public Hp8673B(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;

        public double MinFrequencyMHz => 2000.0;
        public double MaxFrequencyMHz => 26500.0;

        /// <summary>Every HP-IB mnemonic sent, in order.</summary>
        public IReadOnlyList<string> History => _history;

        private void Send(string command)
        {
            _session.Write(command);
            _history.Add(command);
        }

        public void Initialize()
        {
            _session.Clear();   // GPIB device clear
            Send("IP");         // instrument preset
            Send("RF0");        // RF off until we set up
        }

        public void Preset() => Send("IP");

        public void SetFrequencyMHz(double mhz) =>
            Send("FR " + mhz.ToString("0.######", CultureInfo.InvariantCulture) + " MZ");

        public void SetPowerDbm(double dbm) =>
            Send("LE " + dbm.ToString("0.###", CultureInfo.InvariantCulture) + " DM");

        public void RfOn() => Send("RF1");

        public void RfOff() => Send("RF0");
    }
}
