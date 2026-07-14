using System;
using GpibUtils.Instruments.Counters;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.Counters.Tests
{
    /// <summary>Drives the <see cref="Hp5342A"/> driver against a simulated 5342A over the standard transport.</summary>
    public class Hp5342ATests
    {
        private static (Hp5342A driver, Hp5342ASimulatedDevice sim, IInstrumentSession session) Bench()
        {
            var provider = new SimulatedGpibProvider();
            var sim = new Hp5342ASimulatedDevice();
            provider.Add(Hp5342A.DefaultResource, sim.Instrument);
            var session = provider.Open(Hp5342A.DefaultResource);
            return (new Hp5342A(session), sim, session);
        }

        [Fact]
        public void Default_resource_is_gpib_2()
        {
            Assert.Equal("GPIB0::2::INSTR", Hp5342A.DefaultResource);
        }

        [Fact]
        public void Initialize_resets_and_selects_auto()
        {
            var (driver, _, session) = Bench();
            using (session)
            {
                driver.Initialize();
                Assert.Equal<string>(new[] { "RE", "AU" }, driver.History);
            }
        }

        [Fact]
        public void Manual_center_frequency_is_integer_MHz_with_E()
        {
            var (driver, _, session) = Bench();
            using (session)
            {
                driver.SetManualMode();
                driver.SetManualCenterFrequencyMHz(10000);   // 10 GHz
                Assert.Contains("MA", driver.History);
                Assert.Contains("SM10000E", driver.History);
            }
        }

        [Fact]
        public void Center_frequency_over_18ghz_throws()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.Throws<ArgumentOutOfRangeException>(() => driver.SetManualCenterFrequencyMHz(18000));
        }

        [Theory]
        [InlineData(Hp5342AResolution.Hz1, "SR3")]
        [InlineData(Hp5342AResolution.kHz1, "SR6")]
        [InlineData(Hp5342AResolution.MHz1, "SR9")]
        public void Resolution_codes(Hp5342AResolution res, string code)
        {
            Assert.Equal(code, Hp5342A.ResolutionCode(res));
            var (driver, _, session) = Bench();
            using (session)
            {
                driver.SetResolution(res);
                Assert.Equal(code, Assert.Single(driver.History));
            }
        }

        [Fact]
        public void ReadFrequency_talks_the_reading()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.Frequency = 12.4e9;
                Assert.Equal(12.4e9, driver.ReadFrequency(), 0);
            }
        }

        [Fact]
        public void Over_level_dashes_throw()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.OverLevel = true;
                Assert.Throws<InvalidOperationException>(() => driver.ReadFrequency());
            }
        }

        [Fact]
        public void ParseFrequency_rejects_dashes()
        {
            Assert.Throws<InvalidOperationException>(() => Hp5342A.ParseFrequency("----------"));
            Assert.Equal(5.25e9, Hp5342A.ParseFrequency("+5.25000000E+09"), 0);
        }
    }
}
