using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.SignalSources
{
    /// <summary>
    /// Driver for the HP 3245A Universal Source — a pre-SCPI HP-IB precision DC voltage/current source and
    /// low-frequency waveform generator (1 or 2 channels). Uses the instrument's keyword language
    /// (<c>APPLY DCV</c>/<c>APPLY DCI</c>, <c>USE</c>, <c>ARANGE</c>, <c>OUTPUT?</c>, <c>RST</c>, <c>ID?</c> —
    /// there is no <c>*</c>-prefixed 488.2 command). Implements <see cref="IUniversalSource"/> (issue #105);
    /// modulation/triggered-array/waveform features live as extra methods on the concrete class. Runs over any
    /// <see cref="IInstrumentSession"/>.
    ///
    /// <para>Completion for triggered/waveform work maps onto the shared <c>GpibUtils.Visa.Srq</c> engine via
    /// the 3245A's custom status register (bits 0,2,3,4,5,6) and <c>RQS</c> mask; the plain DC setters here are
    /// immediate. The DC ranges (±10.25 V, ±0.1 A) are enforced per the Operating Manual.</para>
    /// </summary>
    public sealed class Hp3245A : IUniversalSource
    {
        /// <summary>GPIB address of the 3245A — factory-set to 09 (confirmed in the Operating Manual, Ch. 2).
        /// Override with <c>--address</c>.</summary>
        public const string DefaultResource = "GPIB0::9::INSTR";

        /// <summary>DC voltage output limit (±V) per the Operating Manual.</summary>
        public const double MaxVoltage = 10.25;

        /// <summary>DC current output limit (±A) per the Operating Manual.</summary>
        public const double MaxCurrent = 0.1;

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public Hp3245A(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;
        public IReadOnlyList<string> History => _history;

        private void Send(string command) { _session.Write(command); _history.Add(command); }

        /// <summary>Queries the model identity (<c>ID?</c> → e.g. <c>HP3245</c>); the 3245A has no <c>*IDN?</c>.</summary>
        public string Identify() { _history.Add("ID?"); return _session.Query("ID?"); }

        /// <summary>Device clear + reset (<c>RST</c>).</summary>
        public void Initialize() { _session.Clear(); Send("RST"); }

        public void Reset() => Send("RST");

        /// <summary>Selects the active channel (<c>USE 0</c> = Channel A, <c>USE 100</c> = Channel B).</summary>
        public void SelectChannel(UniversalSourceChannel channel) =>
            Send("USE " + (channel == UniversalSourceChannel.ChannelB ? "100" : "0"));

        /// <summary>Sets a DC voltage output (<c>APPLY DCV &lt;volts&gt;</c>); ±10.25 V limit.</summary>
        public void SetDcVoltage(double volts)
        {
            if (Math.Abs(volts) > MaxVoltage)
                throw new ArgumentOutOfRangeException(nameof(volts), volts, "DC voltage must be within ±10.25 V.");
            Send("APPLY DCV " + volts.ToString("0.######", CultureInfo.InvariantCulture));
        }

        /// <summary>Sets a DC current output (<c>APPLY DCI &lt;amps&gt;</c>); ±0.1 A limit.</summary>
        public void SetDcCurrent(double amps)
        {
            if (Math.Abs(amps) > MaxCurrent)
                throw new ArgumentOutOfRangeException(nameof(amps), amps, "DC current must be within ±0.1 A.");
            Send("APPLY DCI " + amps.ToString("0.#########", CultureInfo.InvariantCulture));
        }

        /// <summary>Enables/disables output autoranging (<c>ARANGE ON</c>/<c>ARANGE OFF</c>).</summary>
        public void SetAutorange(bool on) => Send("ARANGE " + (on ? "ON" : "OFF"));

        /// <summary>Reads back the programmed output level of the active channel (<c>OUTPUT?</c>).</summary>
        public double ReadOutput()
        {
            _history.Add("OUTPUT?");
            var raw = _session.Query("OUTPUT?");
            if (!double.TryParse((raw ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                throw new FormatException($"Unrecognized 3245A OUTPUT? reading: '{raw}'.");
            return v;
        }

        /// <summary>Sets a sine (AC) voltage output (<c>APPLY ACV &lt;amplitude&gt;</c>) — a "Defined Waveform"
        /// beyond the core interface.</summary>
        public void SetAcVoltage(double amplitude) =>
            Send("APPLY ACV " + amplitude.ToString("0.######", CultureInfo.InvariantCulture));

        /// <summary>Arms the RQS mask (<c>RQS &lt;value&gt;</c>) — the 3245A's SRQ enable, for the shared Srq
        /// engine (decimal sum of status-bit weights, e.g. <c>RQS 48</c> unmasks READY|ERROR).</summary>
        public void SetRequestServiceMask(int mask) =>
            Send("RQS " + mask.ToString(CultureInfo.InvariantCulture));
    }
}
