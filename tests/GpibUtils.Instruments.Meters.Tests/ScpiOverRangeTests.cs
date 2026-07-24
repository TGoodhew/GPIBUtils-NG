using System;
using Xunit;

namespace GpibUtils.Instruments.Meters.Tests
{
    /// <summary>Every SCPI-family meter parser must reject the IEEE-488.2 / SCPI ±9.9E37 over-range / NaN
    /// sentinel rather than return it as a finite reading (#226).</summary>
    public class ScpiOverRangeTests
    {
        [Theory]
        [InlineData("+9.90000000E+37")]
        [InlineData("9.9E37")]
        [InlineData("-9.9E37")]
        public void Hp34401A_rejects_over_range(string raw)
            => Assert.Throws<InvalidOperationException>(() => Hp34401A.ParseReading(raw));

        [Fact]
        public void Hp34401A_reading_list_rejects_over_range_in_a_burst()
            => Assert.Throws<InvalidOperationException>(() => Hp34401A.ParseReadingList("+1.0E+00,+9.9E+37,+3.0E+00"));

        [Fact]
        public void Hp3458A_rejects_over_range()
            => Assert.Throws<InvalidOperationException>(() => Hp3458A.ParseReading("9.9E37"));

        [Fact]
        public void HpE4418B_rejects_over_range()
            => Assert.Throws<InvalidOperationException>(() => HpE4418B.ParsePower("9.90000000E+37"));

        [Fact]
        public void RigolDm3058_rejects_over_range()
            => Assert.Throws<InvalidOperationException>(() => RigolDm3058.ParseReading("+9.9E+37"));

        [Fact]
        public void Keithley2015_rejects_over_range()
            => Assert.Throws<InvalidOperationException>(() => Keithley2015.ParseReading("9.9E37,0,1"));

        [Fact]
        public void Keithley2015_reading_list_rejects_over_range()
            => Assert.Throws<InvalidOperationException>(() => Keithley2015.ParseReadingList("1.0,9.9E37,3.0"));

        [Fact]
        public void Hp8508A_scalar_rejects_over_range()
            => Assert.Throws<InvalidOperationException>(() => Hp8508A.ParseScalar("9.9E37", "AC"));

        [Fact]
        public void Hp8508A_array_rejects_over_range()
            => Assert.Throws<InvalidOperationException>(() => Hp8508A.ParseArray("1.0,9.9E37", "AC"));

        [Theory]
        [InlineData("+1.04530000E-03", 0.0010453)]
        [InlineData("1E37", 1e37)]
        public void Finite_readings_still_parse(string raw, double expected)
            => Assert.Equal(expected, Hp34401A.ParseReading(raw), 9);
    }
}
