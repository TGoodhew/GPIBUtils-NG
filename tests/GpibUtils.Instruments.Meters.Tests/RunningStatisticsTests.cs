using System.Linq;
using GpibUtils.Instruments.Meters;
using Xunit;

namespace GpibUtils.Instruments.Meters.Tests
{
    /// <summary>Tests that the incremental <see cref="RunningStatistics"/> matches the batch
    /// <see cref="DmmStatistics"/> over the same data, so the live monitor's numbers equal the burst's.</summary>
    public class RunningStatisticsTests
    {
        [Fact]
        public void Empty_reports_zero_count_and_nan()
        {
            var r = new RunningStatistics();
            Assert.Equal(0, r.Count);
            Assert.True(double.IsNaN(r.Average));
            Assert.True(double.IsNaN(r.Min));
            Assert.True(double.IsNaN(r.Last));
        }

        [Fact]
        public void Single_value_has_zero_stddev()
        {
            var r = new RunningStatistics();
            r.Add(4.2);
            Assert.Equal(1, r.Count);
            Assert.Equal(4.2, r.Average, 12);
            Assert.Equal(4.2, r.Min, 12);
            Assert.Equal(4.2, r.Max, 12);
            Assert.Equal(4.2, r.Last, 12);
            Assert.Equal(0.0, r.StdDev, 12);
        }

        [Fact]
        public void Matches_batch_statistics_over_the_same_values()
        {
            var values = new[] { 1.0, 2.5, -3.0, 4.25, 0.0, 9.9, -1.1, 2.2, 3.3, 100.0 };

            var running = new RunningStatistics();
            foreach (var v in values) running.Add(v);

            var batch = DmmStatistics.Of(values);

            Assert.Equal(batch.Count, running.Count);
            Assert.Equal(batch.Min, running.Min, 10);
            Assert.Equal(batch.Max, running.Max, 10);
            Assert.Equal(batch.Average, running.Average, 10);
            Assert.Equal(batch.StdDev, running.StdDev, 10);
        }

        [Fact]
        public void Reset_clears_state()
        {
            var r = new RunningStatistics();
            r.Add(1); r.Add(2); r.Add(3);
            r.Reset();
            Assert.Equal(0, r.Count);
            r.Add(10);
            Assert.Equal(10, r.Average, 12);
            Assert.Equal(1, r.Count);
        }

        [Fact]
        public void Snapshot_captures_current_values()
        {
            var r = new RunningStatistics();
            foreach (var v in new[] { 2.0, 4.0, 6.0 }) r.Add(v);
            var snap = r.Snapshot();
            Assert.Equal(3, snap.Count);
            Assert.Equal(4.0, snap.Average, 12);
            Assert.Equal(2.0, snap.Min, 12);
            Assert.Equal(6.0, snap.Max, 12);
        }
    }
}
