using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.SignalSources
{
    /// <summary>
    /// Driver for the Rohde &amp; Schwarz SME signal-generator family (SME02/03/03E/03A/06) — a SCPI
    /// (IEEE-488.2) instrument. CW frequency/power/RF-on-off over any <see cref="IInstrumentSession"/>
    /// (issue #137).
    /// </summary>
    public sealed class RohdeSchwarzSme : ISignalSource
    {
        /// <summary>GPIB address of the SME — factory default 28 (manual-confirmed). Override with
        /// <c>--address</c>.</summary>
        public const string DefaultResource = "GPIB0::28::INSTR";

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public RohdeSchwarzSme(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;
        public IReadOnlyList<string> History => _history;

        private void Send(string command) { _session.Write(command); _history.Add(command); }

        public string Identify() { _history.Add("*IDN?"); return (_session.Query("*IDN?") ?? string.Empty).Trim(); }

        public void Initialize() { _session.Clear(); Send("*RST"); Send("*CLS"); RfOff(); }
        public void Preset() => Send("*RST");

        public void SetFrequencyMHz(double mhz) =>
            Send(":SOURce:FREQuency:CW " + (mhz * 1e6).ToString("G17", CultureInfo.InvariantCulture) + " Hz");

        public void SetPowerDbm(double dbm) =>
            Send(":SOURce:POWer:LEVel " + dbm.ToString("0.###", CultureInfo.InvariantCulture) + " dBm");

        public void RfOn() => Send(":OUTPut:STATe ON");
        public void RfOff() => Send(":OUTPut:STATe OFF");
    }
}
