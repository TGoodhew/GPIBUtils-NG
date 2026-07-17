using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.SignalSources.Tests
{
    public class Hp8663ATests
    {
        private static IInstrumentSession Open()
        {
            var provider = new SimulatedGpibProvider();
            provider.Add(Hp8663A.DefaultResource, new SimulatedInstrument { IdentificationString = "HP8663A" });
            return provider.Open(Hp8663A.DefaultResource);
        }

        [Fact]
        public void Implements_signal_source()
        {
            using (var s = Open())
            {
                var d = new Hp8663A(s);
                Assert.IsAssignableFrom<ISignalSource>(d);
                Assert.Contains("8663A", d.Identify());
            }
        }

        [Fact]
        public void Set_frequency_uses_MZ_suffix()
        {
            using (var s = Open())
            {
                var d = new Hp8663A(s);
                d.SetFrequencyMHz(1279.9);
                Assert.Equal("FR1279.9MZ", Assert.Single(d.History));
            }
        }

        [Fact]
        public void Set_power_uses_DM_suffix()
        {
            using (var s = Open())
            {
                var d = new Hp8663A(s);
                d.SetPowerDbm(-30);
                Assert.Equal("AP-30DM", Assert.Single(d.History));
            }
        }

        [Fact]
        public void RfOff_mutes_and_RfOn_restores_last_amplitude()
        {
            using (var s = Open())
            {
                var d = new Hp8663A(s);
                d.SetPowerDbm(-10);
                d.RfOff();
                d.RfOn();
                Assert.Equal(new[] { "AP-10DM", "AP-140DM", "AP-10DM" }, d.History);
            }
        }

        [Fact]
        public void Request_service_mask_uses_at1()
        {
            using (var s = Open())
            {
                var d = new Hp8663A(s);
                d.SetRequestServiceMask(0x20);   // Sweep End
                Assert.Equal("@132", Assert.Single(d.History));
            }
        }
    }
}
