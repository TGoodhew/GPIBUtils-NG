using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.SignalSources
{
    /// <summary>
    /// Driver for the Agilent/Keysight E4436B ESG-D signal generator — a SCPI instrument, close sibling of the
    /// already-migrated E4438C (minus the ARB). CW frequency/power/RF-on-off over any
    /// <see cref="IInstrumentSession"/> (issue #103). The manuals in the folder are front-panel guides with no
    /// SCPI chapter, so the exact mnemonics are carried over from the E4438C sibling and flagged for bench
    /// confirmation.
    /// </summary>
    public sealed class AgilentE4436B : ISignalSource
    {
        /// <summary>GPIB address of the E4436B — factory default 19 (manual-confirmed). Override with
        /// <c>--address</c>. Never trust bus-scan discovery behind HP-IB extenders.</summary>
        public const string DefaultResource = "GPIB0::19::INSTR";

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public AgilentE4436B(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;
        public IReadOnlyList<string> History => _history;

        private void Send(string command) { _session.Write(command); _history.Add(command); }

        public string Identify() { _history.Add("*IDN?"); return (_session.Query("*IDN?") ?? string.Empty).Trim(); }

        public void Initialize() { _session.Clear(); Send("*RST"); Send("*CLS"); RfOff(); }
        public void Preset() => Send("*RST");

        public void SetFrequencyMHz(double mhz) =>
            Send(":FREQuency:FIXed " + (mhz * 1e6).ToString("G17", CultureInfo.InvariantCulture) + " Hz");

        public void SetPowerDbm(double dbm) =>
            Send(":POWer:LEVel " + dbm.ToString("0.###", CultureInfo.InvariantCulture) + " dBm");

        public void RfOn() => Send(":OUTPut:STATe ON");
        public void RfOff() => Send(":OUTPut:STATe OFF");
    }
}
