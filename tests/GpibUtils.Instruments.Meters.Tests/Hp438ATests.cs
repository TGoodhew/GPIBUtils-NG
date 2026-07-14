using System;
using GpibUtils.Instruments.Meters;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.Meters.Tests
{
    /// <summary>Drives the <see cref="Hp438A"/> driver against a simulated 438A over the standard transport.</summary>
    public class Hp438ATests
    {
        private static (Hp438A driver, Hp438ASimulatedDevice sim, IInstrumentSession session) Bench()
        {
            var provider = new SimulatedGpibProvider();
            var sim = new Hp438ASimulatedDevice();
            provider.Add(Hp438A.DefaultResource, sim.Instrument);
            var session = provider.Open(Hp438A.DefaultResource);
            return (new Hp438A(session), sim, session);
        }

        [Fact]
        public void Default_resource_is_factory_gpib_13()
        {
            Assert.Equal("GPIB0::13::INSTR", Hp438A.DefaultResource);
        }

        [Fact]
        public void Is_a_power_meter()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.IsAssignableFrom<IPowerMeter>(driver);
        }

        [Fact]
        public void Initialize_clears_presets_and_selects_log()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.Initialize();
                Assert.Equal<string>(new[] { "CS", "PR", "LG" }, driver.History);
            }
        }

        [Fact]
        public void Zero_sends_ZE()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.ZeroAndCalibrate();
                Assert.Equal("ZE", Assert.Single(driver.History));
                Assert.True(sim.Zeroed);
            }
        }

        [Fact]
        public void Measure_channel_A_reads_dbm()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.PowerDbmA = -10.5;
                Assert.Equal(-10.5, driver.MeasurePowerDbm(), 4);
                Assert.Contains("AP TR2", driver.History);
            }
        }

        [Fact]
        public void Measure_channel_B_reads_dbm()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.PowerDbmB = 3.25;
                Assert.Equal(3.25, driver.MeasurePowerDbm('B'), 4);
                Assert.Contains("BP TR2", driver.History);
            }
        }

        [Fact]
        public void Over_range_sentinel_throws()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.OverRange = true;
                Assert.Throws<InvalidOperationException>(() => driver.MeasurePowerDbm());
            }
        }

        [Fact]
        public void Invalid_channel_throws()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.Throws<ArgumentOutOfRangeException>(() => driver.MeasurePowerDbm('C'));
        }

        [Fact]
        public void SetCalFactor_sends_percent()
        {
            var (driver, _, session) = Bench();
            using (session)
            {
                driver.SetCalFactorPercent(98.5);
                Assert.Equal("KB 98.5 PCT", Assert.Single(driver.History));
            }
        }

        [Fact]
        public void ParseReading_flags_sentinel()
        {
            Assert.Throws<InvalidOperationException>(() => Hp438A.ParseReading("9.1E+40"));
            Assert.Equal(-12.0, Hp438A.ParseReading("-1.2E+01"), 4);
        }
    }
}
