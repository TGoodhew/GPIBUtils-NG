using System;
using System.Globalization;
using System.Threading;
using GpibUtils.Instruments.SignalSources;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.SignalSources.Tests
{
    public class Hp8340BTests
    {
        // Wires the driver to a simulated 8340B over the standard transport, so assertions exercise the
        // real write path with no hardware. The simulated device decodes the HP-IB mnemonics back into
        // frequency / power / RF state, confirming the exact bytes reached the wire.
        private static (Hp8340B driver, Hp8340BSimulatedDevice sim, IInstrumentSession session) Bench()
        {
            var provider = new SimulatedGpibProvider();
            var sim = new Hp8340BSimulatedDevice();
            provider.Add(Hp8340B.DefaultResource, sim.Instrument);
            var session = provider.Open(Hp8340B.DefaultResource);
            return (new Hp8340B(session), sim, session);
        }

        [Fact]
        public void Is_a_signal_source()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.IsAssignableFrom<ISignalSource>(driver);
        }

        [Theory]
        [InlineData(3000, "CW 3000 MZ")]
        [InlineData(1234.5, "CW 1234.5 MZ")]
        [InlineData(0.01, "CW 0.01 MZ")]
        [InlineData(18000, "CW 18000 MZ")]
        public void SetFrequency_formats_MHz_mnemonic(double mhz, string expected)
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
        [InlineData(-10, "PL -10 DB")]
        [InlineData(0, "PL 0 DB")]
        [InlineData(2.5, "PL 2.5 DB")]
        public void SetPower_formats_dBm_mnemonic(double dbm, string expected)
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
        public void Preset_sends_IP()
        {
            var (driver, _, session) = Bench();
            using (session)
            {
                driver.Preset();
                Assert.Equal("IP", Assert.Single(driver.History));
            }
        }

        [Fact]
        public void Initialize_presets_and_turns_rf_off()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.RfOn();          // leave it on so Initialize has something to clear
                driver.Initialize();
                Assert.False(sim.RfOn);
                Assert.Equal(new[] { "RF1", "IP", "RF0" }, driver.History);
            }
        }

        [Fact]
        public void Cw_setup_sequence_lands_frequency_power_and_rf_on_the_wire()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.Initialize();
                driver.SetFrequencyMHz(2500);
                driver.SetPowerDbm(-5);
                driver.RfOn();

                Assert.Equal(2500, sim.FrequencyMHz);
                Assert.Equal(-5, sim.PowerDbm);
                Assert.True(sim.RfOn);
            }
        }

        [Fact]
        public void Frequency_formatting_is_culture_invariant()
        {
            var original = Thread.CurrentThread.CurrentCulture;
            try
            {
                // A culture that uses ',' as the decimal separator must not leak into the mnemonic.
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                var (driver, sim, session) = Bench();
                using (session)
                {
                    driver.SetFrequencyMHz(1234.5);
                    Assert.Equal("CW 1234.5 MZ", Assert.Single(driver.History));
                    Assert.Equal(1234.5, sim.FrequencyMHz);
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
            Assert.Throws<ArgumentNullException>(() => new Hp8340B(null));
        }
    }
}
