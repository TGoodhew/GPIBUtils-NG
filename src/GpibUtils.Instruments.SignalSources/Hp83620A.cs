using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.SignalSources
{
    /// <summary>
    /// Driver for the HP/Agilent 83620A Synthesized Swept-Signal Generator (10 MHz–20 GHz) — a native SCPI
    /// instrument. Driven here as a CW source (frequency/power/RF-on-off) over any
    /// <see cref="IInstrumentSession"/> (issue #119). Core parameter sets are write-only; the instrument also
    /// supports the standard <c>*OPC?</c>/<c>*SRE</c> completion flow for stepped sweeps (not used by the CW path).
    /// </summary>
    public sealed class Hp83620A : ISignalSource
    {
        /// <summary>GPIB address of the 83620A — factory default 19. Override with <c>--address</c>.</summary>
        public const string DefaultResource = "GPIB0::19::INSTR";

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public Hp83620A(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;
        public IReadOnlyList<string> History => _history;

        private void Send(string command) { _session.Write(command); _history.Add(command); }

        public string Identify() { _history.Add("*IDN?"); return (_session.Query("*IDN?") ?? string.Empty).Trim(); }

        public void Initialize() { _session.Clear(); Send("*RST"); Send("FREQuency:MODE CW"); RfOff(); }
        public void Preset() => Send("*RST");

        public void SetFrequencyMHz(double mhz) =>
            Send("FREQuency:CW " + (mhz * 1e6).ToString("G17", CultureInfo.InvariantCulture) + " HZ");

        public void SetPowerDbm(double dbm) =>
            Send("POWer:LEVel " + dbm.ToString("0.###", CultureInfo.InvariantCulture) + " DBM");

        public void RfOn() => Send("POWer:STATe ON");
        public void RfOff() => Send("POWer:STATe OFF");
    }
}
