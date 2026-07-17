using System;
using GpibUtils.Instruments.Audio;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.Audio.Tests
{
    /// <summary>Drives the <see cref="Hp8903B"/> driver against a simulated 8903B, including the
    /// Special-Function-22 / data-ready SRQ measurement handshake (a custom non-<c>*SRE</c> enable command +
    /// custom bit table through the shared #43/#96 engine).</summary>
    public class Hp8903BTests
    {
        private static (Hp8903B driver, Hp8903BSimulatedDevice sim, IInstrumentSession session) Bench()
        {
            var provider = new SimulatedGpibProvider();
            var sim = new Hp8903BSimulatedDevice();
            provider.Add(Hp8903B.DefaultResource, sim.Instrument);
            var session = provider.Open(Hp8903B.DefaultResource);
            var driver = new Hp8903B(session) { MeasureTimeoutMs = 2000, PollIntervalMs = 5 };
            return (driver, sim, session);
        }

        [Fact]
        public void Is_an_audio_analyzer()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.IsAssignableFrom<IAudioAnalyzer>(driver);
        }

        [Fact]
        public void Default_address_is_factory_twenty_eight()
        {
            Assert.Equal("GPIB0::28::INSTR", Hp8903B.DefaultResource);
        }

        [Fact]
        public void Identify_returns_descriptor()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.Contains("8903B", driver.Identify());
        }

        [Fact]
        public void Initialize_sends_automatic_operation()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.Initialize();
                Assert.Contains("AU", sim.Commands);
            }
        }

        [Fact]
        public void Source_and_measurement_program_codes()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetSourceFrequencyHz(1000);
                driver.SetSourceAmplitude(1.5, AudioAmplitudeUnit.Volts);
                driver.SetMeasurement(AudioMeasurement.Distortion);
                driver.SetDetector(AudioDetector.Average);
                Assert.Contains("FR1000HZ", sim.Commands);
                Assert.Contains("AP1.5V", sim.Commands);
                Assert.Contains("M3", sim.Commands);
                Assert.Contains("A1", sim.Commands);
            }
        }

        [Fact]
        public void Measure_arms_sf22_holds_triggers_and_reads()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.MeasurementValue = 0.012345;
                double v = driver.Measure();
                Assert.Equal(0.012345, v, 6);
                Assert.Contains("22.5SP", sim.Commands);   // SF22 SRQ enable (data-ready|instr-error)
                Assert.Contains("T1", sim.Commands);       // Hold
                Assert.Contains("T3", sim.Commands);       // settled trigger (arm)
            }
        }

        [Fact]
        public void Measure_throws_on_instrument_error()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.ErrorOnMeasure = true;
                var ex = Assert.Throws<Hp8903BException>(() => driver.Measure());
                Assert.False(ex.IsTimeout);
            }
        }

        [Fact]
        public void Measure_times_out_when_never_completes()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.MeasurementCompletes = false;
                driver.MeasureTimeoutMs = 200;
                var ex = Assert.Throws<Hp8903BException>(() => driver.Measure());
                Assert.True(ex.IsTimeout);
            }
        }

        [Fact]
        public void Parse_reading_handles_scientific_and_error_values()
        {
            Assert.Equal(1.2345, Hp8903B.ParseReading("1.23450E+00"), 5);
            Assert.Throws<Hp8903BException>(() => Hp8903B.ParseReading("9.00120E+09"));   // error-valued output
        }

        [Fact]
        public void Status_model_uses_sf22_enable_and_data_ready()
        {
            var model = Hp8903B.StatusModel();
            Assert.True(model.SrqSupported);
            Assert.Equal("22.{mask}SP", model.EnableMask.SetCommand);   // Special Function 22 (custom, non-*SRE)
            Assert.Equal("22.2SP", model.EnableMask.ClearCommand);
            Assert.Equal("requestService", model.RequestServiceBit);
            Assert.Equal(1, model.BitValue("dataReady"));
            Assert.Equal("T3", model.Operations["measurement"].Arm);
            Assert.Equal("dataReady", model.Operations["measurement"].ExpectBit);
        }
    }
}
