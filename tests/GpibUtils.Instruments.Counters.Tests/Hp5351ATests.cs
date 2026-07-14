using System;
using GpibUtils.Instruments.Counters;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.Counters.Tests
{
    /// <summary>Drives the <see cref="Hp5351A"/> driver against a simulated 5351A over the standard transport.</summary>
    public class Hp5351ATests
    {
        private static (Hp5351A driver, Hp5351ASimulatedDevice sim, IInstrumentSession session) Bench()
        {
            var provider = new SimulatedGpibProvider();
            var sim = new Hp5351ASimulatedDevice();
            provider.Add(Hp5351A.DefaultResource, sim.Instrument);
            var session = provider.Open(Hp5351A.DefaultResource);
            return (new Hp5351A(session), sim, session);
        }

        [Fact]
        public void Default_resource_is_gpib_14()
        {
            Assert.Equal("GPIB0::14::INSTR", Hp5351A.DefaultResource);
        }

        [Fact]
        public void Initialize_clears_srqmask_and_presets()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.Initialize();
                Assert.Equal<string>(new[] { "SRQMASK,0", "INIT" }, driver.History);
                Assert.Contains("INIT", sim.Commands);
            }
        }

        [Theory]
        [InlineData(CounterSampleMode.Hold, "SAMPLE,HOLD")]
        [InlineData(CounterSampleMode.Fast, "SAMPLE,FAST")]
        public void SetSampleMode_sends_the_right_command(CounterSampleMode mode, string expected)
        {
            var (driver, _, session) = Bench();
            using (session)
            {
                driver.SetSampleMode(mode);
                Assert.Equal(expected, Assert.Single(driver.History));
            }
        }

        [Fact]
        public void ReadFrequency_talks_the_reading()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.Frequency = 10.5e9;   // 10.5 GHz
                driver.SetSampleMode(CounterSampleMode.Hold);
                Assert.Equal(10.5e9, driver.ReadFrequency(), 0);
            }
        }

        [Fact]
        public void Oven_and_reference_status()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.Oven = "WARM";
                sim.Reference = "EXT";
                Assert.Equal("WARM", driver.OvenStatus());
                Assert.Equal("EXT", driver.ReferenceSource());
            }
        }

        [Fact]
        public void ParseFrequency_parses_scientific()
        {
            Assert.Equal(1.2e10, Hp5351A.ParseFrequency("+1.20000000E+10"), 0);
        }
    }
}
