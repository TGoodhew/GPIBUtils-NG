using System;
using System.Linq;
using System.Text;
using GpibUtils.Instruments.Analyzers;
using Xunit;

namespace GpibUtils.Instruments.Analyzers.Tests
{
    /// <summary>Unit-tests the offline 85620A SRAM-image decoder (#14): bit de-scramble + DLP extraction.</summary>
    public class Hp85620ASramImageTests
    {
        [Theory]
        [InlineData(0x00, 0x00)]
        [InlineData(0xFF, 0xFF)]   // all-ones is a fixed point of the bit permutation
        [InlineData(0x01, 0x02)]
        [InlineData(0x80, 0x01)]
        public void Descramble_byte_permutes_bits(int input, int expected)
        {
            Assert.Equal((byte)expected, Hp85620ASramImage.DescrambleByte((byte)input));
        }

        [Fact]
        public void Translate_address_zero_is_zero()
        {
            Assert.Equal(0, Hp85620ASramImage.TranslateAddress(0));
        }

        [Fact]
        public void Translate_address_matches_the_documented_permutation()
        {
            // Spot-check one bit: position 1 maps into bit 10 (1024) per AddrXlat.
            Assert.Equal(1024, Hp85620ASramImage.TranslateAddress(1));
        }

        [Fact]
        public void Extract_dlps_returns_bodies_between_markers()
        {
            var data = new byte[] { 0x10, 0x80 }
                .Concat(Encoding.ASCII.GetBytes("ABC")).Concat(new byte[] { 0x3B, 0xFF })
                .Concat(new byte[] { 0x00, 0x55 })                                   // gap
                .Concat(new byte[] { 0x10, 0x80 }).Concat(Encoding.ASCII.GetBytes("XY")).Concat(new byte[] { 0x3B, 0xFF })
                .ToArray();

            var dlps = Hp85620ASramImage.ExtractDlps(data);
            Assert.Equal(2, dlps.Count);
            Assert.Equal("ABC", Encoding.ASCII.GetString(dlps[0]));
            Assert.Equal("XY", Encoding.ASCII.GetString(dlps[1]));
        }

        [Fact]
        public void Extract_dlps_ignores_an_unterminated_start()
        {
            var data = new byte[] { 0x10, 0x80, 0x41, 0x42 };   // start marker, no end marker
            Assert.Empty(Hp85620ASramImage.ExtractDlps(data));
        }

        [Fact]
        public void Extract_dlps_on_empty_is_empty()
        {
            Assert.Empty(Hp85620ASramImage.ExtractDlps(Array.Empty<byte>()));
            Assert.Empty(Hp85620ASramImage.ExtractDlps(null));
        }

        [Fact]
        public void Descramble_throws_on_a_too_small_image()
        {
            // Position 1 translates to source address 1024, out of range for a 4-byte buffer.
            Assert.Throws<FormatException>(() => Hp85620ASramImage.Descramble(new byte[4]));
        }

        [Fact]
        public void Find_sequence_locates_and_reports_absence()
        {
            var data = new byte[] { 1, 2, 3, 0x3B, 0xFF, 9 };
            Assert.Equal(3, Hp85620ASramImage.FindSequence(data, new byte[] { 0x3B, 0xFF }, 0));
            Assert.Equal(-1, Hp85620ASramImage.FindSequence(data, new byte[] { 0x3B, 0xFF }, 4));
        }
    }
}
