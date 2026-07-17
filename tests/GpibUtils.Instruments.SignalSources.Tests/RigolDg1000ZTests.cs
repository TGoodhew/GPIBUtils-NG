using GpibUtils.Instruments.SignalSources;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.SignalSources.Tests
{
    public class RigolDg1000ZTests
    {
        private static (RigolDg1000Z driver, RigolDg1000ZSimulatedDevice sim, IInstrumentSession session) Bench()
        {
            var provider = new SimulatedGpibProvider();
            var sim = new RigolDg1000ZSimulatedDevice();
            provider.Add(RigolDg1000Z.DefaultResource, sim.Instrument);
            var session = provider.Open(RigolDg1000Z.DefaultResource);
            return (new RigolDg1000Z(session), sim, session);
        }

        [Fact]
        public void Is_a_function_generator()
        {
            var (driver, _, session) = Bench();
            using (session) Assert.IsAssignableFrom<IFunctionGenerator>(driver);
        }

        [Fact]
        public void Default_address_is_factory_two() => Assert.Equal("GPIB0::2::INSTR", RigolDg1000Z.DefaultResource);

        [Fact]
        public void Identify_uses_idn()
        {
            var (driver, _, session) = Bench();
            using (session) Assert.Contains("DG1062Z", driver.Identify());
        }

        [Fact]
        public void Channel_one_waveform_and_output()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetWaveform(FunctionWaveform.Pulse);
                driver.SetAmplitudeVpp(3);
                driver.OutputOn();
                Assert.Contains(":SOUR1:FUNC PULS", driver.History);
                Assert.Contains(":OUTP1 ON", driver.History);
                Assert.Equal("PULS", sim.Shape(1));
                Assert.Equal(3, sim.Amplitude(1), 3);
                Assert.True(sim.OutputOn(1));
            }
        }

        [Fact]
        public void Selected_channel_two_addresses_the_second_output()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SelectedChannel = 2;
                driver.SetFrequencyHz(2500);
                driver.OutputOn();
                Assert.Contains(":SOUR2:FREQ 2500", driver.History);
                Assert.Contains(":OUTP2 ON", driver.History);
                Assert.Equal(2500, sim.Frequency(2), 3);
                Assert.True(sim.OutputOn(2));
                Assert.False(sim.OutputOn(1));
            }
        }
    }
}
