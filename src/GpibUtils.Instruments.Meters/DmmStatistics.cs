using System;
using System.Collections.Generic;
using System.Globalization;

namespace GpibUtils.Instruments.Meters
{
    /// <summary>
    /// Summary statistics over a set of DMM readings — min, max, mean and sample standard deviation.
    /// Ported from the <c>HP435B-Test</c> recorder-output test, which averaged a 100-point burst per
    /// range-switch position. Use <see cref="Of"/> to compute them from a burst returned by
    /// <see cref="Hp34401A.ReadValues"/>.
    /// </summary>
    public readonly struct DmmStatistics
    {
        public double Min { get; }
        public double Max { get; }
        public double Average { get; }

        /// <summary>Sample (n−1) standard deviation; 0 for a single reading.</summary>
        public double StdDev { get; }

        /// <summary>Number of readings the statistics were computed from.</summary>
        public int Count { get; }

        public DmmStatistics(double min, double max, double average, double stdDev, int count)
        {
            Min = min;
            Max = max;
            Average = average;
            StdDev = stdDev;
            Count = count;
        }

        /// <summary>
        /// Computes statistics over <paramref name="values"/> in a single pass (Welford's algorithm for a
        /// numerically stable variance).
        /// </summary>
        public static DmmStatistics Of(IEnumerable<double> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));

            double min = double.PositiveInfinity, max = double.NegativeInfinity;
            double mean = 0.0, m2 = 0.0;
            int n = 0;

            foreach (var v in values)
            {
                n++;
                if (v < min) min = v;
                if (v > max) max = v;
                double delta = v - mean;
                mean += delta / n;
                m2 += delta * (v - mean);
            }

            if (n == 0) throw new ArgumentException("No values to summarize.", nameof(values));

            double stdDev = n > 1 ? Math.Sqrt(m2 / (n - 1)) : 0.0;
            return new DmmStatistics(min, max, mean, stdDev, n);
        }

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture,
                "n={0} min={1:G6} max={2:G6} avg={3:G6} sd={4:G6}", Count, Min, Max, Average, StdDev);
    }
}
