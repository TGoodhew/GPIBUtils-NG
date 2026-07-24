using System;
using System.Globalization;

namespace GpibUtils.Visa
{
    /// <summary>
    /// Helpers for turning a SCPI / IEEE-488.2 numeric measurement response into a usable <see cref="double"/>,
    /// applying the rule every measurement parser in the suite must share: the ±9.9E37 over-range / no-result
    /// sentinel (and any non-finite value) is <b>not</b> a real reading and must be rejected rather than logged
    /// as a finite number. IEEE-488.2 §7.7.2.1 / SCPI define <c>9.9E37</c> as the "not a number" result an
    /// instrument returns for over-range or an uncomputable measurement; a bench CSV/TUI cannot distinguish a
    /// logged <c>9.9e37</c> from a genuine large reading, and a FAIL then reads as "measured 9.9e37" instead of
    /// "instrument over-range".
    /// </summary>
    public static class ScpiReading
    {
        /// <summary>The IEEE-488.2 / SCPI over-range / "not a number" sentinel magnitude. A parsed value whose
        /// magnitude is at or beyond this is the instrument's over-range / no-result indication.</summary>
        public const double OverRangeSentinel = 9.9e37;

        /// <summary>True when <paramref name="v"/> is not a usable measurement: NaN, ±∞, or at/beyond the SCPI
        /// ±9.9E37 over-range sentinel. (Uses <see cref="double.IsNaN"/>/<see cref="double.IsInfinity"/> —
        /// <c>double.IsFinite</c> does not exist on net472.)</summary>
        public static bool IsOverRange(double v) =>
            double.IsNaN(v) || double.IsInfinity(v) || Math.Abs(v) >= OverRangeSentinel;

        /// <summary>
        /// Parses a single SCPI numeric measurement field and rejects the over-range/NaN sentinel. Throws
        /// <see cref="FormatException"/> when <paramref name="raw"/> is empty or not a number, or
        /// <see cref="InvalidOperationException"/> when it parses to the ±9.9E37 sentinel / a non-finite value.
        /// <paramref name="instrument"/> names the device in the messages; the raw text is always echoed.
        /// </summary>
        public static double Parse(string raw, string instrument = null)
        {
            string who = string.IsNullOrEmpty(instrument) ? "instrument" : instrument;
            if (string.IsNullOrWhiteSpace(raw))
                throw new FormatException($"Empty {who} reading.");
            if (!double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                throw new FormatException($"Unrecognized {who} reading: '{raw}'.");
            return Guard(v, raw.Trim(), who);
        }

        /// <summary>
        /// Guards an already-parsed value: returns it unchanged, or throws <see cref="InvalidOperationException"/>
        /// when it is the SCPI over-range/NaN sentinel. Use when the number was extracted by the caller (a
        /// first-field split, a regex over a compound response) and only the sentinel check is needed;
        /// <paramref name="raw"/> is echoed for diagnostics.
        /// </summary>
        public static double Guard(double v, string raw, string instrument = null)
        {
            if (IsOverRange(v))
            {
                string who = string.IsNullOrEmpty(instrument) ? "instrument" : instrument;
                throw new InvalidOperationException(
                    $"{who} returned an over-range / no-result value (SCPI ±9.9E37 sentinel): '{raw}'.");
            }
            return v;
        }
    }
}
