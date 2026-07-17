using System;
using GpibUtils.Instruments.SignalSources;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.SignalSources.Tests
{
    public class Hp8116ATests
    {
        private static (Hp8116A driver, Hp8116ASimulatedDevice sim, IInstrumentSession session) Bench()
        {
            var provider = new SimulatedGpibProvider();
            var sim = new Hp8116ASimulatedDevice();
            provider.Add(Hp8116A.DefaultResource, sim.Instrument);
            var session = provider.Open(Hp8116A.DefaultResource);
            return (new Hp8116A(session), sim, session);
        }

        [Fact]
        public void Is_a_function_generator()
        {
            var (driver, _, session) = Bench();
            using (session) Assert.IsAssignableFrom<IFunctionGenerator>(driver);
        }

        [Fact]
        public void Default_address_is_factory_sixteen() => Assert.Equal("GPIB0::16::INSTR", Hp8116A.DefaultResource);

        [Fact]
        public void Frequency_amplitude_offset_use_mnemonics_with_units()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetFrequencyHz(1000);
                driver.SetAmplitudeVpp(2);
                driver.SetOffsetVolts(0.12);
                Assert.Contains("FRQ 1000 HZ", driver.History);
                Assert.Contains("AMP 2 V", driver.History);
                Assert.Contains("OFS 0.12 V", driver.History);
                Assert.Equal(1000, sim.FrequencyHz, 3);
                Assert.Equal(2, sim.AmplitudeVpp, 3);
                Assert.Equal(0.12, sim.OffsetVolts, 3);
            }
        }

        [Fact]
        public void Output_toggles_via_disable_codes()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.OutputOn();
                Assert.True(sim.OutputEnabled);
                Assert.Contains("D0", driver.History);
                driver.OutputOff();
                Assert.False(sim.OutputEnabled);
                Assert.Contains("D1", driver.History);
            }
        }

        [Fact]
        public void Waveform_select_is_not_supported_pending_bench()
        {
            var (driver, _, session) = Bench();
            using (session) Assert.Throws<NotSupportedException>(() => driver.SetWaveform(FunctionWaveform.Sine));
        }

        [Fact]
        public void Fault_surfaces_via_status_byte_and_ierr()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.InjectFault(0x04 | 0x40, "WIDTH ERROR");   // syntax error + SRQ
                Assert.True(driver.HasFault());
                Assert.Equal("WIDTH ERROR", driver.ReadError());
            }
        }
    }
}
