using System;
using GpibUtils.Visa;
using Xunit;

namespace GpibUtils.Visa.Tests
{
    public class ScpiReadingTests
    {
        [Theory]
        [InlineData("+1.04530000E-03", 0.0010453)]
        [InlineData("-10", -10.0)]
        [InlineData("  1.5e3 ", 1500.0)]
        public void Parse_returns_finite_readings(string raw, double expected)
        {
            Assert.Equal(expected, ScpiReading.Parse(raw, "test"), 9);
        }

        [Theory]
        [InlineData("9.9E37")]
        [InlineData("+9.90000000E+37")]
        [InlineData("-9.9E37")]
        [InlineData("1E38")]
        public void Parse_rejects_the_over_range_sentinel(string raw)
        {
            var ex = Assert.Throws<InvalidOperationException>(() => ScpiReading.Parse(raw, "test"));
            Assert.Contains(raw.Trim(), ex.Message);   // the raw text is echoed
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("N/A")]
        public void Parse_rejects_empty_or_non_numeric(string raw)
        {
            Assert.Throws<FormatException>(() => ScpiReading.Parse(raw, "test"));
        }

        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        [InlineData(9.9e37)]
        [InlineData(-1e40)]
        public void IsOverRange_flags_non_finite_and_sentinel(double v)
        {
            Assert.True(ScpiReading.IsOverRange(v));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(1e37)]
        [InlineData(-1234.5)]
        public void IsOverRange_passes_real_values(double v)
        {
            Assert.False(ScpiReading.IsOverRange(v));
            Assert.Equal(v, ScpiReading.Guard(v, v.ToString(), "test"));
        }

        [Fact]
        public void Guard_throws_on_the_sentinel_and_echoes_raw()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => ScpiReading.Guard(9.9e37, "9.9E37", "34401A"));
            Assert.Contains("34401A", ex.Message);
            Assert.Contains("9.9E37", ex.Message);
        }
    }
}
