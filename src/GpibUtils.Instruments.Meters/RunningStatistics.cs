using System;

namespace GpibUtils.Instruments.Meters
{
    /// <summary>
    /// Incremental min/max/mean/sample-standard-deviation over a stream of DMM readings, updated one value
    /// at a time (Welford's algorithm) in O(1) per sample with no growing buffer. The batch sibling
    /// <see cref="DmmStatistics"/> computes the same quantities over a fixed set; this accumulates them live
    /// for the continuous monitor exposed identically by all three front-ends (CLI <c>monitor</c>, the TUI
    /// DMM dashboard, and the WPF DMM tab) — one shared implementation keeps their numbers in step.
    /// </summary>
    public sealed class RunningStatistics
    {
        private double _mean;
        private double _m2;
        private double _min = double.PositiveInfinity;
        private double _max = double.NegativeInfinity;

        /// <summary>Number of readings accumulated so far.</summary>
        public int Count { get; private set; }

        /// <summary>The most recently added reading (NaN before the first).</summary>
        public double Last { get; private set; } = double.NaN;

        /// <summary>Smallest reading (NaN before the first).</summary>
        public double Min => Count == 0 ? double.NaN : _min;

        /// <summary>Largest reading (NaN before the first).</summary>
        public double Max => Count == 0 ? double.NaN : _max;

        /// <summary>Running mean (NaN before the first).</summary>
        public double Average => Count == 0 ? double.NaN : _mean;

        /// <summary>Sample (n−1) standard deviation; 0 for a single reading, NaN before the first.</summary>
        public double StdDev => Count == 0 ? double.NaN : (Count > 1 ? Math.Sqrt(_m2 / (Count - 1)) : 0.0);

        /// <summary>Adds a reading, updating every statistic in one pass.</summary>
        public void Add(double value)
        {
            Count++;
            Last = value;
            if (value < _min) _min = value;
            if (value > _max) _max = value;
            double delta = value - _mean;
            _mean += delta / Count;
            _m2 += delta * (value - _mean);
        }

        /// <summary>Clears all accumulated state.</summary>
        public void Reset()
        {
            _mean = 0;
            _m2 = 0;
            _min = double.PositiveInfinity;
            _max = double.NegativeInfinity;
            Count = 0;
            Last = double.NaN;
        }

        /// <summary>Takes an immutable snapshot as a <see cref="DmmStatistics"/> (call only when
        /// <see cref="Count"/> &gt; 0).</summary>
        public DmmStatistics Snapshot() => new DmmStatistics(Min, Max, Average, StdDev, Count);
    }
}
