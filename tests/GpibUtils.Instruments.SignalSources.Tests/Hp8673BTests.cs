using System;
using System.Globalization;
using System.Threading;
using GpibUtils.Instruments.SignalSources;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.SignalSources.Tests
{
    public class Hp8673BTests
    {
        private static (Hp8673B driver, Hp8673BSimulatedDevice sim, IInstrumentSession session) Bench()
        {
            var provider = new SimulatedGpibProvider();
            var sim = new Hp8673BSimulatedDevice();
            provider.Add(Hp8673B.DefaultResource, sim.Instrument);
            var session = provider.Open(Hp8673B.DefaultResource);
            return (new Hp8673B(session), sim, session);
        }

        [Fact]
        public void Is_a_local_oscillator_with_stated_range()
        {
            var (driver, _, session) = Bench();
            using (session)
            {
                Assert.IsAssignableFrom<ILocalOscillator>(driver);
                Assert.Equal(2000.0, driver.MinFrequencyMHz);
                Assert.Equal(26500.0, driver.MaxFrequencyMHz);
            }
        }

        [Theory]
        [InlineData(10000, "FR 10000 MZ")]
        [InlineData(2000, "FR 2000 MZ")]
        [InlineData(26500, "FR 26500 MZ")]
        [InlineData(12345.678, "FR 12345.678 MZ")]
        public void SetFrequency_uses_FR_MZ_mnemonic(double mhz, string expected)
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetFrequencyMHz(mhz);
                Assert.Equal(expected, Assert.Single(driver.History));
                Assert.Equal(mhz, sim.FrequencyMHz);
            }
        }

        [Theory]
        [InlineData(8, "LE 8 DM")]
        [InlineData(-10, "LE -10 DM")]
        [InlineData(2.5, "LE 2.5 DM")]
        public void SetPower_uses_LE_DM_mnemonic(double dbm, string expected)
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetPowerDbm(dbm);
                Assert.Equal(expected, Assert.Single(driver.History));
                Assert.Equal(dbm, sim.PowerDbm);
            }
        }

        [Fact]
        public void RfOn_and_RfOff_drive_output_state()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.RfOn();
                Assert.True(sim.RfOn);
                driver.RfOff();
                Assert.False(sim.RfOn);
                Assert.Equal(new[] { "RF1", "RF0" }, driver.History);
            }
        }

        [Fact]
        public void Initialize_presets_and_turns_rf_off()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.RfOn();
                driver.Initialize();
                Assert.False(sim.RfOn);
                Assert.Equal(new[] { "RF1", "IP", "RF0" }, driver.History);
            }
        }

        [Fact]
        public void Cw_setup_sequence_lands_frequency_level_and_rf_on_the_wire()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.Initialize();
                driver.SetFrequencyMHz(18000);
                driver.SetPowerDbm(10);
                driver.RfOn();

                Assert.Equal(18000, sim.FrequencyMHz);
                Assert.Equal(10, sim.PowerDbm);
                Assert.True(sim.RfOn);
            }
        }

        [Fact]
        public void Frequency_formatting_is_culture_invariant()
        {
            var original = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                var (driver, sim, session) = Bench();
                using (session)
                {
                    driver.SetFrequencyMHz(12345.678);
                    Assert.Equal("FR 12345.678 MZ", Assert.Single(driver.History));
                    Assert.Equal(12345.678, sim.FrequencyMHz);
                }
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }

        [Fact]
        public void Null_session_throws()
        {
            Assert.Throws<ArgumentNullException>(() => new Hp8673B(null));
        }
    }
}
