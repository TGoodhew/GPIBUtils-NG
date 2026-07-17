using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.SignalSources
{
    /// <summary>
    /// Driver for the HP 8664A Synthesized Signal Generator — speaks HP-SL ("HP Signal-generator Language"),
    /// HP's IEEE-488.2-compliant colon/semicolon subsystem-tree command language. CW frequency/power/RF-on-off
    /// over any <see cref="IInstrumentSession"/> (issue #125). NOTE: factory default address 19 clashes with the
    /// 8673B/8340B — remap one of them on a shared bus.
    /// </summary>
    public sealed class Hp8664A : ISignalSource
    {
        /// <summary>GPIB address of the 8664A — factory default 19 (clashes with 8673B/8340B; remap on a
        /// shared bus). Override with <c>--address</c>.</summary>
        public const string DefaultResource = "GPIB0::19::INSTR";

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public Hp8664A(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;
        public IReadOnlyList<string> History => _history;

        private void Send(string command) { _session.Write(command); _history.Add(command); }

        public string Identify() { _history.Add("*IDN?"); return (_session.Query("*IDN?") ?? string.Empty).Trim(); }

        public void Initialize() { _session.Clear(); Send("*RST"); Send("*CLS"); RfOff(); }
        public void Preset() => Send("*RST");

        /// <summary>Sets the CW frequency (<c>FREQ:CW &lt;MHz&gt;MHZ</c>).</summary>
        public void SetFrequencyMHz(double mhz) =>
            Send("FREQ:CW " + mhz.ToString("0.######", CultureInfo.InvariantCulture) + "MHZ");

        /// <summary>Sets the output level (<c>AMPL:LEV &lt;dBm&gt;DBM</c>).</summary>
        public void SetPowerDbm(double dbm) =>
            Send("AMPL:LEV " + dbm.ToString("0.###", CultureInfo.InvariantCulture) + "DBM");

        /// <summary>Gates the RF output on (<c>AMPL:STATe ON</c>).</summary>
        public void RfOn() => Send("AMPL:STATe ON");

        /// <summary>Gates the RF output off (<c>AMPL:STATe OFF</c>).</summary>
        public void RfOff() => Send("AMPL:STATe OFF");
    }
}
