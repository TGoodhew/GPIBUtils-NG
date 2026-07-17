using System;
using GpibUtils.Instruments.SignalSources;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.SignalSources.Tests
{
    public class Hp33120ATests
    {
        private static (Hp33120A driver, Hp33120ASimulatedDevice sim, IInstrumentSession session) Bench()
        {
            var provider = new SimulatedGpibProvider();
            var sim = new Hp33120ASimulatedDevice();
            provider.Add(Hp33120A.DefaultResource, sim.Instrument);
            var session = provider.Open(Hp33120A.DefaultResource);
            return (new Hp33120A(session), sim, session);
        }

        [Fact]
        public void Is_a_function_generator()
        {
            var (driver, _, session) = Bench();
            using (session) Assert.IsAssignableFrom<IFunctionGenerator>(driver);
        }

        [Fact]
        public void Default_address_is_factory_ten() => Assert.Equal("GPIB0::10::INSTR", Hp33120A.DefaultResource);

        [Fact]
        public void Identify_uses_idn()
        {
            var (driver, _, session) = Bench();
            using (session) Assert.Contains("33120A", driver.Identify());
        }

        [Fact]
        public void Waveform_frequency_amplitude_offset()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetWaveform(FunctionWaveform.Square);
                driver.SetFrequencyHz(1000);
                driver.SetAmplitudeVpp(2);
                driver.SetOffsetVolts(0.5);
                Assert.Contains("FUNC:SHAP SQU", driver.History);
                Assert.Contains("FREQ 1000", driver.History);
                Assert.Contains("VOLT 2", driver.History);
                Assert.Contains("VOLT:OFFS 0.5", driver.History);
                Assert.Equal("SQU", sim.Shape);
                Assert.Equal(1000, sim.FrequencyHz, 3);
                Assert.Equal(2, sim.AmplitudeVpp, 3);
                Assert.Equal(0.5, sim.OffsetVolts, 3);
            }
        }

        [Fact]
        public void Pulse_waveform_is_not_supported()
        {
            var (driver, _, session) = Bench();
            using (session) Assert.Throws<NotSupportedException>(() => driver.SetWaveform(FunctionWaveform.Pulse));
        }
    }
}
