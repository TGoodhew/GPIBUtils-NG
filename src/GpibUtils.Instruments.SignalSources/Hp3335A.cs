using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.SignalSources
{
    /// <summary>
    /// Driver for the HP 3335A Synthesizer/Level Generator (200 Hz–80 MHz) — a pre-SCPI HP-IB source driven
    /// by single-character front-panel key codes (issue #107). Sets frequency (<c>F&lt;value&gt;&lt;unit&gt;</c>)
    /// and amplitude (<c>A&lt;value&gt;&lt;unit&gt;</c>) where the unit key doubles as the entry terminator:
    /// for frequency <c>M</c>=MHz, <c>K</c>=kHz, <c>H</c>=Hz; for amplitude <c>K</c>=+dBm, <c>M</c>=−dBm.
    /// Runs over any <see cref="IInstrumentSession"/>.
    ///
    /// <para>The 3335A is <b>listen-only</b> (interface function T0, no talker capability): it cannot be
    /// addressed to talk, serial-polled, or queried — there is no <c>*IDN?</c> or any readback, so
    /// <see cref="Identify"/> returns a descriptor and command effect can only be verified from the
    /// instrument's own front-panel display. It has <b>no remote RF on/off key</b>; the only remote mute is a
    /// very negative amplitude — hence this is deliberately <b>not</b> an <see cref="ISignalSource"/>
    /// (same decision as the HP 8350B).</para>
    /// </summary>
    public sealed class Hp3335A
    {
        /// <summary>GPIB address of the 3335A — set by a rear-panel switch with no documented factory default
        /// (the instrument is listen-only). 11 is used here as a placeholder — confirm at the bench and
        /// override with <c>--address</c>.</summary>
        public const string DefaultResource = "GPIB0::11::INSTR";

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public Hp3335A(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;
        public IReadOnlyList<string> History => _history;

        private void Send(string command) { _session.Write(command); _history.Add(command); }

        /// <summary>The 3335A is listen-only (no <c>*IDN?</c>, no readback); returns a fixed descriptor.</summary>
        public string Identify() => "HP 3335A Synthesizer/Level Generator (listen-only, no readback)";

        /// <summary>Device clear (the 3335A responds to DCL/SDC).</summary>
        public void Initialize() => _session.Clear();
        public void Preset() => _session.Clear();

        /// <summary>Sets the output frequency in Hz (<c>F&lt;value&gt;H</c>).</summary>
        public void SetFrequencyHz(double hz) =>
            Send("F" + hz.ToString("0.###", CultureInfo.InvariantCulture) + "H");

        /// <summary>Sets the output frequency in MHz (<c>F&lt;value&gt;M</c>).</summary>
        public void SetFrequencyMHz(double mhz) =>
            Send("F" + mhz.ToString("0.######", CultureInfo.InvariantCulture) + "M");

        /// <summary>Sets the output amplitude in dBm. The unit key encodes the sign: <c>K</c>=+dBm for
        /// non-negative levels, <c>M</c>=−dBm for negative levels (so −10 dBm → <c>A10M</c>).</summary>
        public void SetAmplitudeDbm(double dbm)
        {
            var magnitude = Math.Abs(dbm).ToString("0.###", CultureInfo.InvariantCulture);
            Send("A" + magnitude + (dbm < 0 ? "M" : "K"));
        }
    }
}
