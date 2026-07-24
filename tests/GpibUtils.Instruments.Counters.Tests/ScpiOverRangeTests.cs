using System;
using Xunit;

namespace GpibUtils.Instruments.Counters.Tests
{
    /// <summary>The SCPI counter frequency parsers must reject the ±9.9E37 over-range / NaN sentinel (#226).</summary>
    public class ScpiOverRangeTests
    {
        [Theory]
        [InlineData("9.9E37")]
        [InlineData("+9.90000000E+37")]
        public void Hp5351A_rejects_over_range(string raw)
            => Assert.Throws<InvalidOperationException>(() => Hp5351A.ParseFrequency(raw));

        [Fact]
        public void Hp53131A_rejects_over_range()
            => Assert.Throws<InvalidOperationException>(() => Hp53131A.ParseFrequency("9.9E37"));

        [Fact]
        public void Finite_frequency_still_parses()
            => Assert.Equal(1.0e7, Hp5351A.ParseFrequency("+1.00000000E+07"), 0);
    }
}
