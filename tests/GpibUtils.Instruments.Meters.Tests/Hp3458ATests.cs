using System;
using GpibUtils.Instruments.Meters;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.Meters.Tests
{
    /// <summary>Drives the <see cref="Hp3458A"/> driver against a simulated 3458A over the standard transport.</summary>
    public class Hp3458ATests
    {
        private static (Hp3458A driver, Hp3458ASimulatedDevice sim, IInstrumentSession session) Bench()
        {
            var provider = new SimulatedGpibProvider();
            var sim = new Hp3458ASimulatedDevice();
            provider.Add(Hp3458A.DefaultResource, sim.Instrument);
            var session = provider.Open(Hp3458A.DefaultResource);
            return (new Hp3458A(session), sim, session);
        }

        [Fact]
        public void Default_resource_is_factory_gpib_22()
        {
            Assert.Equal("GPIB0::22::INSTR", Hp3458A.DefaultResource);
        }

        [Fact]
        public void Initialize_resets_and_sets_end_always()
        {
            var (driver, _, session) = Bench();
            using (session)
            {
                driver.Initialize();
                Assert.Equal<string>(new[] { "RESET", "END ALWAYS" }, driver.History);
            }
        }

        [Fact]
        public void Identify_uses_ID_query()
        {
            var (driver, _, session) = Bench();
            using (session)
            {
                Assert.Equal("HP3458A", driver.Identify());
                Assert.Contains("ID?", driver.History);
            }
        }

        [Fact]
        public void Configure_dc_voltage_sends_func_dcv()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.ConfigureFunction(Hp3458AFunction.DcVoltage);
                Assert.Equal("FUNC DCV", Assert.Single(driver.History));
                Assert.Equal("DCV", sim.Function);
                Assert.False(sim.AcSync);
            }
        }

        [Fact]
        public void Configure_ac_voltage_adds_setacv_sync()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.ConfigureFunction(Hp3458AFunction.AcVoltage);
                Assert.Equal<string>(new[] { "FUNC ACV", "SETACV SYNC" }, driver.History);
                Assert.True(sim.AcSync);
            }
        }

        [Theory]
        [InlineData(Hp3458AFunction.Resistance4Wire, "OHMF")]
        [InlineData(Hp3458AFunction.DcCurrent, "DCI")]
        [InlineData(Hp3458AFunction.Frequency, "FREQ")]
        public void Func_keyword_mapping(Hp3458AFunction fn, string kw)
        {
            Assert.Equal(kw, Hp3458A.FuncKeyword(fn));
        }

        [Fact]
        public void SetNplc_and_resolution()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetNplc(100);
                driver.SetResolution(0.001);
                Assert.Contains("NPLC 100", driver.History);
                Assert.Contains("RES 0.001", driver.History);
                Assert.Equal(100, sim.Nplc);
            }
        }

        [Fact]
        public void ReadValue_triggers_single_and_parses()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.Reading = 1.23456;
                Assert.Equal(1.23456, driver.ReadValue(), 5);
                Assert.Contains("TARM SGL", driver.History);
            }
        }

        [Fact]
        public void ReadValues_returns_a_burst()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.Reading = 2.0;
                Assert.Equal(new[] { 2.0, 2.0, 2.0 }, driver.ReadValues(3));
            }
        }
    }
}
