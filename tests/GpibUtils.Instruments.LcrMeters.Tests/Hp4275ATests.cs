using System;
using GpibUtils.Instruments.LcrMeters;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.LcrMeters.Tests
{
    /// <summary>Drives the <see cref="Hp4275A"/> driver against a simulated 4275A, including the <c>I1</c>-armed
    /// Data-Ready SRQ measurement handshake (a custom non-<c>*SRE</c> enable command + custom bit table through
    /// the shared #43/#96 engine).</summary>
    public class Hp4275ATests
    {
        private static (Hp4275A driver, Hp4275ASimulatedDevice sim, IInstrumentSession session) Bench()
        {
            var provider = new SimulatedGpibProvider();
            var sim = new Hp4275ASimulatedDevice();
            provider.Add(Hp4275A.DefaultResource, sim.Instrument);
            var session = provider.Open(Hp4275A.DefaultResource);
            var driver = new Hp4275A(session) { MeasureTimeoutMs = 2000, PollIntervalMs = 5 };
            return (driver, sim, session);
        }

        [Fact]
        public void Is_an_lcr_meter()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.IsAssignableFrom<ILcrMeter>(driver);
        }

        [Fact]
        public void Default_address_is_provisional_seventeen()
        {
            Assert.Equal("GPIB0::17::INSTR", Hp4275A.DefaultResource);
        }

        [Fact]
        public void Identify_returns_descriptor()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.Contains("4275A", driver.Identify());
        }

        [Fact]
        public void Program_codes_for_parameter_frequency_circuit()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetPrimaryParameter(LcrParameter.Capacitance);
                driver.SetTestFrequency(LcrFrequency.F100kHz);
                driver.SetCircuitMode(LcrCircuitMode.Series);
                Assert.Contains("A2", sim.Commands);
                Assert.Contains("F14", sim.Commands);
                Assert.Contains("C2", sim.Commands);
            }
        }

        [Fact]
        public void Measure_arms_data_ready_srq_executes_and_reads_both_displays()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.Primary = 1.0e-9;
                sim.Secondary = 2.5e-3;
                var reading = driver.Measure();
                Assert.Equal(1.0e-9, reading.Primary, 12);
                Assert.Equal(2.5e-3, reading.Secondary, 6);
                Assert.Contains("I1", sim.Commands);   // Data-Ready SRQ armed (custom enable command)
                Assert.Contains("E", sim.Commands);    // Execute (arm)
            }
        }

        [Fact]
        public void Measure_throws_on_instrument_error()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.ErrorOnMeasure = true;
                var ex = Assert.Throws<Hp4275AException>(() => driver.Measure());
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
                var ex = Assert.Throws<Hp4275AException>(() => driver.Measure());
                Assert.True(ex.IsTimeout);
            }
        }

        [Fact]
        public void Parse_reading_extracts_two_values()
        {
            var reading = Hp4275A.ParseReading("A1.2340E-09,B5.0000E-03");
            Assert.Equal(1.234e-9, reading.Primary, 12);
            Assert.Equal(5.0e-3, reading.Secondary, 6);
        }

        [Fact]
        public void Status_model_uses_data_ready_enable_and_execute_arm()
        {
            var model = Hp4275A.StatusModel();
            Assert.True(model.SrqSupported);
            Assert.Equal("I1", model.EnableMask.SetCommand);   // Data-Ready SRQ enable (custom, non-*SRE)
            Assert.Equal("I0", model.EnableMask.ClearCommand);
            Assert.Equal("requestService", model.RequestServiceBit);
            Assert.Equal(1, model.BitValue("dataReady"));
            Assert.Equal("E", model.Operations["measurement"].Arm);
            Assert.Equal("dataReady", model.Operations["measurement"].ExpectBit);
        }
    }
}
