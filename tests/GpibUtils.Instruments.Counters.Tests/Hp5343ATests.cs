using System;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.Counters.Tests
{
    public class Hp5343ATests
    {
        private static IInstrumentSession Open(string reading = null)
        {
            var provider = new SimulatedGpibProvider();
            provider.Add(Hp5343A.DefaultResource, new SimulatedInstrument
            {
                IdentificationString = "HP5343A",
                Responder = _ => reading
            });
            return provider.Open(Hp5343A.DefaultResource);
        }

        [Fact]
        public void Identifies_without_idn()
        {
            using (var s = Open())
                Assert.Contains("5343A", new Hp5343A(s).Identify());
        }

        [Fact]
        public void Initialize_resets_and_selects_auto()
        {
            using (var s = Open())
            {
                var d = new Hp5343A(s);
                d.Initialize();
                Assert.Equal(new[] { "R", "AU" }, d.History);
            }
        }

        [Fact]
        public void Manual_center_frequency_uses_SM_E()
        {
            using (var s = Open())
            {
                var d = new Hp5343A(s);
                d.SetManualMode();
                d.SetManualCenterFrequencyMHz(10000);
                Assert.Equal(new[] { "M", "SM10000E" }, d.History);
            }
        }

        [Fact]
        public void Center_frequency_above_range_throws()
        {
            using (var s = Open())
                Assert.Throws<ArgumentOutOfRangeException>(() => new Hp5343A(s).SetManualCenterFrequencyMHz(27000));
        }

        [Theory]
        [InlineData(Hp5343AResolution.Hz1, "SR3")]
        [InlineData(Hp5343AResolution.kHz1, "SR6")]
        [InlineData(Hp5343AResolution.MHz1, "SR9")]
        public void Resolution_codes(Hp5343AResolution resolution, string code)
        {
            Assert.Equal(code, Hp5343A.ResolutionCode(resolution));
        }

        [Fact]
        public void Read_frequency_parses_fixed_format_to_hz()
        {
            // 10 GHz: mantissa 10000.000000 MHz scaled by E06.
            using (var s = Open(" F  10000.000000 E 06"))
            {
                var d = new Hp5343A(s);
                Assert.Equal(1.0e10, d.ReadFrequency(), 0);
            }
        }

        [Fact]
        public void Overload_dashes_throw()
        {
            using (var s = Open(" F  ------------ E 06"))
                Assert.Throws<InvalidOperationException>(() => new Hp5343A(s).ReadFrequency());
        }

        [Fact]
        public void Display_overflow_throws()
        {
            using (var s = Open(" F  99999.999999 E 06"))
                Assert.Throws<InvalidOperationException>(() => new Hp5343A(s).ReadFrequency());
        }

        [Fact]
        public void Insufficient_signal_throws()
        {
            using (var s = Open(" F  00000.000000 E 06"))
                Assert.Throws<InvalidOperationException>(() => new Hp5343A(s).ReadFrequency());
        }
    }
}
