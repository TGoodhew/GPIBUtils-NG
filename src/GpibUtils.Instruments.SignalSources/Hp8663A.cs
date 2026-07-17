using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.SignalSources
{
    /// <summary>
    /// Driver for the HP 8663A Synthesized Signal Generator (100 kHz–2560 MHz) — a pre-SCPI, legacy
    /// two-letter mnemonic HP-IB source (issue #124). Sets CW frequency (<c>FR &lt;MHz&gt; MZ</c>) and
    /// amplitude (<c>AP &lt;dBm&gt; DM</c>); Device Clear presets to 100 MHz / −30 dBm / modulation+sweep off.
    /// Implements <see cref="ISignalSource"/> following the same pattern as <c>KeysightE4438C</c> (modulation,
    /// sweep and RQS-mask/status live as extra methods on the concrete class). Runs over any
    /// <see cref="IInstrumentSession"/>.
    ///
    /// <para>The 8663A has <b>no dedicated RF on/off code</b> — the front-panel key set has no ON/OFF key, so
    /// <see cref="RfOff"/> mutes by commanding the amplitude to the spec floor and <see cref="RfOn"/> restores
    /// the last commanded amplitude (bench-confirm whether a true RF-off equivalent exists). The default GPIB
    /// address is <b>not documented</b> in the manual (internal thumbwheel switches, readable via Special
    /// Function 82) — confirm at the bench and override with <c>--address</c>.</para>
    /// </summary>
    public sealed class Hp8663A : ISignalSource
    {
        /// <summary>GPIB address of the 8663A — <b>no factory default is documented</b> (internal thumbwheel
        /// switches; read the current value via front-panel Special Function 82). 19 is a common HP-synthesizer
        /// default and is used here as a placeholder — confirm at the bench and override with <c>--address</c>.</summary>
        public const string DefaultResource = "GPIB0::19::INSTR";

        /// <summary>Amplitude (dBm) commanded by <see cref="RfOff"/> to mute the output (spec floor).</summary>
        public const double MuteDbm = -140.0;

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();
        private double _powerDbm = -30.0;   // the Device-Clear preset amplitude

        public Hp8663A(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;
        public IReadOnlyList<string> History => _history;

        private void Send(string command) { _session.Write(command); _history.Add(command); }

        /// <summary>The 8663A predates <c>*IDN?</c>; returns a fixed descriptor.</summary>
        public string Identify() => "HP 8663A Synthesized Signal Generator (no *IDN?)";

        /// <summary>Device clear presets to 100 MHz / −30 dBm / modulation+sweep off.</summary>
        public void Initialize() { _session.Clear(); _powerDbm = -30.0; }
        public void Preset() { _session.Clear(); _powerDbm = -30.0; }

        /// <summary>Sets the CW carrier frequency (<c>FR &lt;MHz&gt; MZ</c>).</summary>
        public void SetFrequencyMHz(double mhz) =>
            Send("FR" + mhz.ToString("0.######", CultureInfo.InvariantCulture) + "MZ");

        /// <summary>Sets the output amplitude (<c>AP &lt;dBm&gt; DM</c>).</summary>
        public void SetPowerDbm(double dbm)
        {
            _powerDbm = dbm;
            Send(Amplitude(dbm));
        }

        /// <summary>Restores the last commanded amplitude (no dedicated RF-on code — see class remarks).</summary>
        public void RfOn() => Send(Amplitude(_powerDbm));

        /// <summary>Mutes the output by commanding the amplitude to the spec floor (no dedicated RF-off code).</summary>
        public void RfOff() => Send(Amplitude(MuteDbm));

        private static string Amplitude(double dbm) =>
            "AP" + dbm.ToString("0.###", CultureInfo.InvariantCulture) + "DM";

        /// <summary>Arms the Request-Service (RQS) mask (<c>@1 &lt;byte&gt;</c>) for the given status bits;
        /// this is the 8663A's analogue of <c>*SRE</c>. Maps onto the shared <c>GpibUtils.Visa.Srq</c> engine.</summary>
        public void SetRequestServiceMask(byte mask) =>
            Send("@1" + mask.ToString(CultureInfo.InvariantCulture));
    }
}
