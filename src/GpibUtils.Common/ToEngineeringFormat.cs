using System;

namespace GpibUtils.Common
{
    /// <summary>
    /// Formats a number in engineering notation (powers of ten in multiples of three, with the SI
    /// prefix) — e.g. <c>1.23 kHz</c>, <c>4.70 uF</c>. Consolidates the copy-pasted helper that
    /// appeared across the individual instrument apps (HP435B-Test, E3633A-Demo, DM3058, DS1054Z, ...).
    /// </summary>
    /// <remarks>Algorithm credit: Steve Hageman —
    /// http://analoghome.blogspot.com/2012/01/how-to-format-numbers-in-engineering.html. Hardened here
    /// for zero and for magnitudes outside the yocto..yotta prefix range.</remarks>
    public static class ToEngineeringFormat
    {
        // SI prefixes from 1e-24 (yocto) to 1e+24 (yotta); index 8 is the unscaled (1e0) slot.
        private static readonly string[] Prefixes =
            { " y", " z", " a", " f", " p", " n", " u", " m", " ", " k", " M", " G", " T", " P", " E", " Z", " Y" };
        private const int ZeroPowerIndex = 8;   // position of the " " (1e0) prefix
        private const int MinPower = -8;         // 1e-24
        private const int MaxPower = 8;          // 1e+24

        /// <summary>
        /// Converts <paramref name="number"/> to engineering notation.
        /// </summary>
        /// <param name="number">The value to format.</param>
        /// <param name="significantDigits">Significant digits (clamped to 1..15). Use ≥3 to stay in
        /// engineering notation; fewer may fall back to scientific for the mantissa.</param>
        /// <param name="units">Unit suffix appended after the prefix, e.g. "Hz", "V", "F".</param>
        /// <param name="fixedFormat">When true, uses fixed-point ("F") mantissa formatting instead of
        /// general ("G").</param>
        public static string Convert(double number, short significantDigits = 3, string units = "",
                                     bool fixedFormat = false)
        {
            if (significantDigits < 1) significantDigits = 1;
            if (significantDigits > 15) significantDigits = 15;
            units = units ?? string.Empty;

            // Zero (and non-finite) have no meaningful scale — format the mantissa directly, unscaled.
            if (number == 0.0 || double.IsNaN(number) || double.IsInfinity(number))
            {
                var zeroFmt = (fixedFormat ? "F" : "G") + significantDigits.ToString();
                return number.ToString(zeroFmt) + " " + units;
            }

            double scale = Math.Log10(Math.Abs(number));
            if (scale < 0.0) scale += -3.0;

            // + 0.001 nudges borderline scales into the correct decade.
            int power = (int)((scale / 3) + 0.001);
            if (power < MinPower) power = MinPower;
            if (power > MaxPower) power = MaxPower;

            string prefix = Prefixes[power + ZeroPowerIndex];
            double baseNum = number / Math.Pow(10.0, power * 3.0);

            string mantissaFmt = (fixedFormat ? "F" : "G") + significantDigits.ToString();
            return baseNum.ToString(mantissaFmt) + prefix + units;
        }
    }
}
