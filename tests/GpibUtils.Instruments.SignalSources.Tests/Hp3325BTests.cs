using GpibUtils.Instruments.SignalSources;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.SignalSources.Tests
{
    /// <summary>Drives the <see cref="Hp3325B"/> driver against a simulated 3325B over the standard transport.</summary>
    public class Hp3325BTests
    {
        private static (Hp3325B driver, Hp3325BSimulatedDevice sim, IInstrumentSession session) Bench()
        {
            var provider = new SimulatedGpibProvider();
            var sim = new Hp3325BSimulatedDevice();
            provider.Add(Hp3325B.DefaultResource, sim.Instrument);
            var session = provider.Open(Hp3325B.DefaultResource);
            return (new Hp3325B(session), sim, session);
        }

        [Fact]
        public void Default_resource_is_factory_gpib_17()
        {
            Assert.Equal("GPIB0::17::INSTR", Hp3325B.DefaultResource);
        }

        [Theory]
        [InlineData(Hp3325BWaveform.Dc, "FU0")]
        [InlineData(Hp3325BWaveform.Sine, "FU1")]
        [InlineData(Hp3325BWaveform.PositiveRamp, "FU4")]
        public void Waveform_codes(Hp3325BWaveform w, string code)
        {
            Assert.Equal(code, Hp3325B.FunctionCode(w));
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetWaveform(w);
                Assert.Equal(code, sim.Function);
            }
        }

        [Fact]
        public void Frequency_hz_and_mhz()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetFrequencyHz(100);
                Assert.Equal("FR 100 HZ", driver.History[0]);
                Assert.Equal(100, sim.FrequencyHz);
                driver.SetFrequencyMHz(20.999999999);
                Assert.Equal(20999999.999, sim.FrequencyHz.Value, 1);
            }
        }

        [Fact]
        public void Amplitude_and_offset_in_volts()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetAmplitudeVolts(1.0);
                driver.SetDcOffsetVolts(5.0);
                Assert.Equal("AM 1 VO", driver.History[0]);
                Assert.Equal("OF 5 VO", driver.History[1]);
                Assert.Equal(1.0, sim.AmplitudeVolts);
                Assert.Equal(5.0, sim.OffsetVolts);
            }
        }

        [Fact]
        public void Amplitude_calibration_sends_AC()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.AmplitudeCalibration();
                Assert.Equal("AC", Assert.Single(driver.History));
                Assert.True(sim.Calibrated);
            }
        }

        [Fact]
        public void Reset_clears_state()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetFrequencyHz(1000);
                driver.Reset();
                Assert.Null(sim.FrequencyHz);
            }
        }
    }
}
