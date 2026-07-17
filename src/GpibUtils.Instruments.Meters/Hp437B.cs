using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.Meters
{
    /// <summary>
    /// Driver for the HP 437B single-channel RF/microwave power meter — a mnemonic HP-IB instrument that
    /// (unlike the 438A) also supports IEEE-488.2 <c>*IDN?</c>. Presets, zeroes/calibrates, and reads power in
    /// dBm. Reconstructed from the 437B Operating Manual (issue #111), sibling of the migrated 438A. Runs over
    /// any <see cref="IInstrumentSession"/>.
    /// </summary>
    public sealed class Hp437B : IPowerMeter
    {
        /// <summary>GPIB address of the 437B — factory default 13 (as with the 438A sibling; remap one on a
        /// shared bus). Override with <c>--address</c>.</summary>
        public const string DefaultResource = "GPIB0::13::INSTR";

        /// <summary>Readings at or above this magnitude are the meter's Log/over-range error indication.</summary>
        public const double ErrorSentinel = 9e40;

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public Hp437B(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;
        public IReadOnlyList<string> History => _history;

        private void Send(string command) { _session.Write(command); _history.Add(command); }
        private string Query(string command) { _history.Add(command); return (_session.Query(command) ?? string.Empty).Trim(); }

        public string Identify() => Query("*IDN?");

        public void Initialize()
        {
            _session.Clear();
            Send("CS");   // clear status
            Send("PR");   // preset
            Send("LG");   // Log (dBm) mode
        }

        /// <summary>Zeroes the sensor (<c>ZE</c>) then calibrates against a 100% reference cal factor
        /// (<c>CL100EN</c>).</summary>
        public void ZeroAndCalibrate()
        {
            Send("ZE");
            Send("CL100EN");
        }

        /// <summary>Sets the sensor cal factor as a percentage (<c>KB &lt;pct&gt; PCT</c>).</summary>
        public void SetCalFactorPercent(double percent) =>
            Send("KB" + percent.ToString("0.#", CultureInfo.InvariantCulture) + "PCT");

        /// <summary>Triggers a settled reading (<c>TR2</c>) and reads the power in dBm.</summary>
        public double MeasurePowerDbm() => ParseReading(Query("TR2"));

        internal static double ParseReading(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) throw new FormatException("Empty 437B reading.");
            if (!double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                throw new FormatException($"Unrecognized 437B reading: '{raw}'.");
            if (Math.Abs(v) >= ErrorSentinel)
                throw new InvalidOperationException("437B returned an over-range / no-sensor error indication.");
            return v;
        }
    }
}
