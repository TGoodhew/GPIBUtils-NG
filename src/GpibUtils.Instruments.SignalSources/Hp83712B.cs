using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.SignalSources
{
    /// <summary>
    /// Driver for the HP/Agilent 83712B Synthesized CW Generator (0.01–20 GHz) — a native SCPI instrument,
    /// CW-only. Frequency/power/RF-on-off over any <see cref="IInstrumentSession"/> (issue #120), driven
    /// write-only like the sibling <see cref="Hp8340B"/>/<see cref="Hp8673B"/>. NOTE: factory default address
    /// 19 clashes with the 8673B/8340B — remap one of them on a shared bus.
    /// </summary>
    public sealed class Hp83712B : ISignalSource
    {
        /// <summary>GPIB address of the 83712B — factory default 19 (clashes with 8673B/8340B; remap on a
        /// shared bus). Override with <c>--address</c>.</summary>
        public const string DefaultResource = "GPIB0::19::INSTR";

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public Hp83712B(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;
        public IReadOnlyList<string> History => _history;

        private void Send(string command) { _session.Write(command); _history.Add(command); }

        public string Identify() { _history.Add("*IDN?"); return (_session.Query("*IDN?") ?? string.Empty).Trim(); }

        public void Initialize() { _session.Clear(); Send("*RST"); RfOff(); }
        public void Preset() => Send("*RST");

        public void SetFrequencyMHz(double mhz) =>
            Send("FREQ " + (mhz * 1e6).ToString("G17", CultureInfo.InvariantCulture) + " HZ");

        public void SetPowerDbm(double dbm) =>
            Send("POW " + dbm.ToString("0.###", CultureInfo.InvariantCulture) + " DBM");

        public void RfOn() => Send("OUTP:STAT ON");
        public void RfOff() => Send("OUTP:STAT OFF");
    }
}
