using System;
using System.Collections.Generic;
using System.Text;

namespace GpibUtils.Console.Tui
{
    /// <summary>
    /// Renders a compact single-line sparkline from numeric samples using Unicode block characters, for the
    /// TUI DMM live dashboard. Pure (no console) so the scaling is unit-testable. Values are scaled between
    /// the min and max of the shown window; a flat series renders at a mid level.
    /// </summary>
    public static class Sparkline
    {
        private static readonly char[] Blocks = { '▁', '▂', '▃', '▄', '▅', '▆', '▇', '█' };

        /// <summary>Renders the last <paramref name="width"/> of <paramref name="values"/> as block chars.
        /// Empty input yields an empty string; non-finite samples render as a space.</summary>
        public static string Render(IReadOnlyList<double> values, int width)
        {
            if (values == null || values.Count == 0 || width <= 0) return string.Empty;

            int start = Math.Max(0, values.Count - width);
            int count = values.Count - start;

            double min = double.PositiveInfinity, max = double.NegativeInfinity;
            for (int i = start; i < values.Count; i++)
            {
                double v = values[i];
                if (double.IsNaN(v) || double.IsInfinity(v)) continue;
                if (v < min) min = v;
                if (v > max) max = v;
            }

            // No finite samples at all.
            if (double.IsPositiveInfinity(min)) return new string(' ', count);

            double range = max - min;
            var sb = new StringBuilder(count);
            for (int i = start; i < values.Count; i++)
            {
                double v = values[i];
                if (double.IsNaN(v) || double.IsInfinity(v)) { sb.Append(' '); continue; }
                int level = range <= 0
                    ? Blocks.Length / 2
                    : (int)Math.Round((v - min) / range * (Blocks.Length - 1));
                if (level < 0) level = 0;
                if (level >= Blocks.Length) level = Blocks.Length - 1;
                sb.Append(Blocks[level]);
            }
            return sb.ToString();
        }
    }
}
