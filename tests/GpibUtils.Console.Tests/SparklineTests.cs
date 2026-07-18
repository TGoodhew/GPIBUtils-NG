using GpibUtils.Console.Tui;
using Xunit;

namespace GpibUtils.Console.Tests
{
    /// <summary>Tests for the TUI DMM dashboard sparkline scaling.</summary>
    public class SparklineTests
    {
        [Fact]
        public void Empty_input_renders_empty()
        {
            Assert.Equal("", Sparkline.Render(new double[0], 10));
            Assert.Equal("", Sparkline.Render(null, 10));
            Assert.Equal("", Sparkline.Render(new[] { 1.0, 2.0 }, 0));
        }

        [Fact]
        public void Ascending_series_maps_low_to_high_blocks()
        {
            var s = Sparkline.Render(new[] { 0.0, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0 }, 8);
            Assert.Equal(8, s.Length);
            Assert.Equal('▁', s[0]);   // minimum → lowest block
            Assert.Equal('█', s[s.Length - 1]); // maximum → highest block
        }

        [Fact]
        public void Flat_series_renders_mid_level_not_crash()
        {
            var s = Sparkline.Render(new[] { 5.0, 5.0, 5.0, 5.0 }, 8);
            Assert.Equal(4, s.Length);
            Assert.All(s, c => Assert.Equal(s[0], c)); // all identical
        }

        [Fact]
        public void Only_last_width_samples_are_shown()
        {
            var s = Sparkline.Render(new[] { 0.0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, 3);
            Assert.Equal(3, s.Length); // last 3 only
        }
    }
}
