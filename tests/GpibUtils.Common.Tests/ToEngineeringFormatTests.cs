using GpibUtils.Common;
using Xunit;

namespace GpibUtils.Common.Tests
{
    public class ToEngineeringFormatTests
    {
        [Theory]
        [InlineData(1234.0, "1.23 kHz")]
        [InlineData(1_500_000.0, "1.5 MHz")]
        [InlineData(0.0047, "4.7 mHz")]
        [InlineData(0.000_002_2, "2.2 uHz")]
        [InlineData(12.0, "12 Hz")]
        public void Formats_common_magnitudes(double value, string expected)
        {
            Assert.Equal(expected, ToEngineeringFormat.Convert(value, 3, "Hz"));
        }

        [Fact]
        public void Zero_is_handled_without_infinity()
        {
            Assert.Equal("0 V", ToEngineeringFormat.Convert(0.0, 3, "V"));
        }

        [Fact]
        public void Negative_values_keep_their_sign()
        {
            Assert.Equal("-2.5 mV", ToEngineeringFormat.Convert(-0.0025, 3, "V"));
        }

        [Fact]
        public void Fixed_format_honours_significant_digits()
        {
            Assert.Equal("1.500 kHz", ToEngineeringFormat.Convert(1500.0, 3, "Hz", fixedFormat: true));
        }

        [Fact]
        public void Extreme_magnitude_does_not_index_out_of_range()
        {
            // Well beyond yotta — must clamp, not throw.
            var result = ToEngineeringFormat.Convert(1e40, 3, "Hz");
            Assert.False(string.IsNullOrWhiteSpace(result));
        }
    }
}
