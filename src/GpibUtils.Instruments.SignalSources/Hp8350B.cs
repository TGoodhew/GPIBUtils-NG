using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.SignalSources
{
    /// <summary>
    /// Driver for the HP 8350B Sweep Oscillator used as a CW source. HP-IB codes (8350B Operating manual):
    /// <c>IP</c> (instrument preset), <c>CW &lt;val&gt; MZ</c> (CW frequency; the suffix selects the unit —
    /// MZ = MHz, GZ = GHz), <c>PL &lt;val&gt; DM</c> (power level in dBm). Driven write-only for CW
    /// frequency and power (the 8350B has no discrete RF on/off — RF is gated by the leveling/blanking of the
    /// installed plug-in), so it does <b>not</b> implement <see cref="ISignalSource"/>. Ported from
    /// <c>GPIBUtils/HPDevices</c> (issue #22). Runs over any <see cref="IInstrumentSession"/>.
    /// </summary>
    public sealed class Hp8350B
    {
        /// <summary>GPIB address of the 8350B — its documented factory HP-IB address is 19 (8350B Operating
        /// manual; the address switches are set to 19). <b>Note this collides with the 8673B's factory 19</b>
        /// (<see cref="Hp8673B.DefaultResource"/>) — on a bench with both, remap one (as the 8340B is remapped
        /// to 20) via <c>config address set hp8350b …</c>. Override with <c>--address</c>.</summary>
        public const string DefaultResource = "GPIB0::19::INSTR";

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public Hp8350B(IInstrumentSession session) =>
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
            _session.Clear();   // HP-IB device clear
            Send("IP");         // instrument preset
        }

        public void Preset() => Send("IP");

        public void SetFrequencyMHz(double mhz) =>
            Send("CW " + mhz.ToString("0.######", CultureInfo.InvariantCulture) + " MZ");

        public void SetPowerDbm(double dbm) =>
            Send("PL " + dbm.ToString("0.###", CultureInfo.InvariantCulture) + " DM");
    }
}
