using GpibUtils.Instruments.SignalSources;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.SignalSources.Tests
{
    /// <summary>Drives the <see cref="Hp8350B"/> driver against a simulated 8350B over the standard transport.</summary>
    public class Hp8350BTests
    {
        private static (Hp8350B driver, Hp8350BSimulatedDevice sim, IInstrumentSession session) Bench()
        {
            var provider = new SimulatedGpibProvider();
            var sim = new Hp8350BSimulatedDevice();
            provider.Add(Hp8350B.DefaultResource, sim.Instrument);
            var session = provider.Open(Hp8350B.DefaultResource);
            return (new Hp8350B(session), sim, session);
        }

        [Fact]
        public void Default_resource_is_gpib_19()
        {
            Assert.Equal("GPIB0::19::INSTR", Hp8350B.DefaultResource);
        }

        [Fact]
        public void Initialize_presets()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.Initialize();
                Assert.Equal("IP", Assert.Single(driver.History));
            }
        }

        [Fact]
        public void Set_frequency_uses_MZ_suffix_and_is_decoded()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetFrequencyMHz(7555);
                Assert.Equal("CW 7555 MZ", Assert.Single(driver.History));
                Assert.Equal(7555, sim.FrequencyMHz);
            }
        }

        [Fact]
        public void Set_power_uses_DM_suffix_and_is_decoded()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetPowerDbm(-5);
                Assert.Equal("PL -5 DM", Assert.Single(driver.History));
                Assert.Equal(-5, sim.PowerDbm);
            }
        }

        [Fact]
        public void Preset_clears_decoded_state()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetFrequencyMHz(1000);
                driver.Preset();
                Assert.Null(sim.FrequencyMHz);
            }
        }
    }
}
