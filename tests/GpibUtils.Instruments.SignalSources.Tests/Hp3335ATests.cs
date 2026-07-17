using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.SignalSources.Tests
{
    public class Hp3335ATests
    {
        private static IInstrumentSession Open()
        {
            var provider = new SimulatedGpibProvider();
            provider.Add(Hp3335A.DefaultResource, new SimulatedInstrument { IdentificationString = "HP3335A" });
            return provider.Open(Hp3335A.DefaultResource);
        }

        [Fact]
        public void Is_not_a_signal_source_and_identifies_as_listen_only()
        {
            using (var s = Open())
            {
                var d = new Hp3335A(s);
                Assert.False(typeof(ISignalSource).IsAssignableFrom(typeof(Hp3335A)));
                Assert.Contains("listen-only", d.Identify());
            }
        }

        [Fact]
        public void Set_frequency_hz_and_mhz()
        {
            using (var s = Open())
            {
                var d = new Hp3335A(s);
                d.SetFrequencyHz(12000);
                d.SetFrequencyMHz(25);
                Assert.Equal(new[] { "F12000H", "F25M" }, d.History);
            }
        }

        [Fact]
        public void Amplitude_encodes_sign_in_unit_key()
        {
            using (var s = Open())
            {
                var d = new Hp3335A(s);
                d.SetAmplitudeDbm(9);     // +dBm -> K
                d.SetAmplitudeDbm(-10);   // -dBm -> M
                Assert.Equal(new[] { "A9K", "A10M" }, d.History);
            }
        }
    }
}
