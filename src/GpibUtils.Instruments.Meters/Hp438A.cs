using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.Meters
{
    /// <summary>
    /// Driver for the HP 438A dual-channel (A/B) RF power meter — a pre-SCPI HP-IB instrument driven by
    /// terse mnemonic commands. Presets, zeroes the sensor, selects Log (dBm) mode, and reads power on a
    /// channel. Reconstructed from the <c>GPIBUtils-Old/HP438A</c> app (issue #33); the mnemonic set is
    /// faithful to that source and should be confirmed at the bench. Runs over any
    /// <see cref="IInstrumentSession"/>.
    ///
    /// <para>The 438A has no <c>*IDN?</c> — <see cref="Identify"/> returns a fixed descriptor. A reading at
    /// or above <see cref="ErrorSentinel"/> is the meter's out-of-range/no-sensor error indication, surfaced
    /// as an <see cref="InvalidOperationException"/>.</para>
    /// </summary>
    public sealed class Hp438A : IPowerMeter
    {
        /// <summary>GPIB address of the 438A — its documented factory-default HP-IB address is 13 (438A
        /// Operating/Service manual: "The internal HP-IB address switch is set at the factory to 13").
        /// Override with <c>--address</c>.</summary>
        public const string DefaultResource = "GPIB0::13::INSTR";

        /// <summary>Readings at or above this value are the 438A's Log/over-range error indication
        /// (the app skipped values &gt; 8e40), not real power.</summary>
        public const double ErrorSentinel = 9e40;

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public Hp438A(IInstrumentSession session) =>
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

        /// <summary>The 438A predates <c>*IDN?</c>; returns a fixed descriptor.</summary>
        public string Identify() => "HP 438A Power Meter (no *IDN? — pre-SCPI HP-IB)";

        public void Initialize()
        {
            _session.Clear();   // HP-IB device clear
            Send("CS");         // clear status byte
            Send("PR");         // preset
            Send("LG");         // Log mode → readings in dBm
        }

        /// <summary>Zeroes the sensor (<c>ZE</c>). (Reference calibration is a front-panel step on the 438A.)</summary>
        public void ZeroAndCalibrate() => Send("ZE");

        /// <summary>Enters the sensor cal factor as a percentage (<c>KB &lt;pct&gt; PCT</c>). The 438A takes a
        /// cal-factor percent, not a frequency — confirm the exact mnemonic on the bench.</summary>
        public void SetCalFactorPercent(double percent) =>
            Send("KB " + percent.ToString("0.#", CultureInfo.InvariantCulture) + " PCT");

        /// <summary>Measures power (dBm) on channel A.</summary>
        public double MeasurePowerDbm() => MeasurePowerDbm('A');

        /// <summary>Measures power (dBm) on channel A or B: selects the sensor and free-run trigger
        /// (<c>{A|B}P TR2</c>), then reads the talked value.</summary>
        public double MeasurePowerDbm(char channel)
        {
            char c = char.ToUpperInvariant(channel);
            if (c != 'A' && c != 'B')
                throw new ArgumentOutOfRangeException(nameof(channel), channel, "Channel must be A or B.");
            return ParseReading(Query(c + "P TR2"));
        }

        /// <summary>Parses a 438A reading; throws when it is the out-of-range/error sentinel.</summary>
        internal static double ParseReading(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                throw new FormatException("Empty 438A reading.");
            if (!double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                throw new FormatException($"Unrecognized 438A reading: '{raw}'.");
            if (Math.Abs(v) >= ErrorSentinel)
                throw new InvalidOperationException(
                    "438A returned an over-range / no-sensor error indication (Log-mode error value).");
            return v;
        }
    }
}
