using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.Counters
{
    /// <summary>Sample-rate mode of the HP 5351A.</summary>
    public enum CounterSampleMode
    {
        /// <summary>Hold — the counter holds each reading until read (<c>SAMPLE,HOLD</c>).</summary>
        Hold,
        /// <summary>Fast — the counter free-runs at its fastest sample rate (<c>SAMPLE,FAST</c>).</summary>
        Fast
    }

    /// <summary>
    /// Driver for the HP 5351A Microwave Frequency Counter (to 26.5 GHz) — a single-input microwave counter
    /// driven with mnemonic HP-IB commands. Presets, selects the sample mode, reads the measured frequency
    /// (Hz), and reports oven / reference status. Ported from the working <c>GPIBUtils/HPDevices</c> driver
    /// (issue #20). Runs over any <see cref="IInstrumentSession"/>.
    /// </summary>
    public sealed class Hp5351A : ILegacyFrequencyCounter
    {
        /// <summary>GPIB address of the 5351A. The counter has no fixed factory-default HP-IB address (it is
        /// set on a rear-panel switch); the legacy test app used <c>GPIB0::14::INSTR</c>. Override with
        /// <c>--address</c>.</summary>
        public const string DefaultResource = "GPIB0::14::INSTR";

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public Hp5351A(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;

        public IReadOnlyList<string> History => _history;

        private void Send(string command)
        {
            _session.Write(command);
            _history.Add(command);
        }

        private string Query(string command)
        {
            _history.Add(command);
            return (_session.Query(command) ?? string.Empty).Trim();
        }

        /// <summary>The 5351A predates <c>*IDN?</c>; returns a fixed descriptor.</summary>
        public string Identify() => "HP 5351A Microwave Frequency Counter (no *IDN?)";

        public void Initialize()
        {
            _session.Clear();       // HP-IB device clear
            Send("SRQMASK,0");      // clear the SRQ mask
            Send("INIT");           // instrument preset
        }

        /// <summary>Selects the sample-rate mode (<c>SAMPLE,HOLD</c> / <c>SAMPLE,FAST</c>).</summary>
        public void SetSampleMode(CounterSampleMode mode) =>
            Send(mode == CounterSampleMode.Hold ? "SAMPLE,HOLD" : "SAMPLE,FAST");

        /// <summary>Reads the measured frequency in Hz (the counter talks its latest reading).</summary>
        public double ReadFrequency() => ParseFrequency(_session.ReadString());

        /// <summary>Reads the oven status string (<c>OVEN?</c>), e.g. WARM / READY.</summary>
        public string OvenStatus() => Query("OVEN?");

        /// <summary>Reads the reference-source status string (<c>REF?</c>), e.g. INT / EXT.</summary>
        public string ReferenceSource() => Query("REF?");

        /// <summary>Parses a 5351A frequency reading (scientific notation, Hz) to a double.</summary>
        internal static double ParseFrequency(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                throw new FormatException("Empty 5351A frequency reading.");
            var s = raw.Trim();
            if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var hz))
            {
                var m = System.Text.RegularExpressions.Regex.Match(s, @"[-+]?[0-9]*\.?[0-9]+([eE][-+]?[0-9]+)?");
                if (!m.Success || !double.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out hz))
                    throw new FormatException($"Unrecognized 5351A frequency reading: '{raw}'.");
            }
            return hz;
        }
    }
}
