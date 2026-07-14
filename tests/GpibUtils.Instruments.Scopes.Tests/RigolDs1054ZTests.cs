using System;
using GpibUtils.Instruments.Scopes;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.Scopes.Tests
{
    /// <summary>Drives the <see cref="RigolDs1054Z"/> driver against a simulated DS1054Z over the standard transport.</summary>
    public class RigolDs1054ZTests
    {
        private static (RigolDs1054Z driver, RigolDs1054ZSimulatedDevice sim, IInstrumentSession session) Bench()
        {
            var provider = new SimulatedGpibProvider();
            var sim = new RigolDs1054ZSimulatedDevice();
            provider.Add(RigolDs1054Z.DefaultResource, sim.Instrument);
            var session = provider.Open(RigolDs1054Z.DefaultResource);
            return (new RigolDs1054Z(session), sim, session);
        }

        [Fact]
        public void Is_an_oscilloscope()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.IsAssignableFrom<IOscilloscope>(driver);
        }

        [Fact]
        public void Run_stop_single_autoscale()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.Run(); Assert.Equal("RUN", sim.RunState);
                driver.Stop(); Assert.Equal("STOP", sim.RunState);
                driver.Single(); Assert.Equal("SINGLE", sim.RunState);
                driver.AutoScale();
                Assert.Contains(":AUToscale", driver.History);
            }
        }

        [Fact]
        public void Channel_display_toggles()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetChannelDisplay(2, true);
                Assert.Equal(":CHANnel2:DISPlay ON", Assert.Single(driver.History));
                Assert.True(sim.ChannelOn(2));
                driver.SetChannelDisplay(2, false);
                Assert.False(sim.ChannelOn(2));
            }
        }

        [Fact]
        public void Measure_vpp_reads_item()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.SetMeasurement("VPP", 1, 3.3);
                Assert.Equal(3.3, driver.MeasureVpp(1), 4);
                Assert.Contains(":MEASure:ITEM? VPP,CHANnel1", driver.History);
            }
        }

        [Fact]
        public void Timebase_scale_query()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.TimebaseScale = 5e-4;
                Assert.Equal(5e-4, driver.TimebaseScaleSeconds(), 9);
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(5)]
        public void Out_of_range_channel_throws(int channel)
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.Throws<ArgumentOutOfRangeException>(() => driver.SetChannelDisplay(channel, true));
        }

        [Fact]
        public void Identify_returns_idn()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.Contains("DS1054Z", driver.Identify());
        }
    }
}
