using System;
using GpibUtils.Instruments.Meters;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.Meters.Tests
{
    /// <summary>Drives the <see cref="RigolDm3058"/> driver against a simulated DM3058 over the standard transport.</summary>
    public class RigolDm3058Tests
    {
        private static (RigolDm3058 driver, RigolDm3058SimulatedDevice sim, IInstrumentSession session) Bench()
        {
            var provider = new SimulatedGpibProvider();
            var sim = new RigolDm3058SimulatedDevice();
            provider.Add(RigolDm3058.DefaultResource, sim.Instrument);
            var session = provider.Open(RigolDm3058.DefaultResource);
            return (new RigolDm3058(session), sim, session);
        }

        [Fact]
        public void Default_resource_is_factory_gpib_7()
        {
            Assert.Equal("GPIB0::7::INSTR", RigolDm3058.DefaultResource);
        }

        [Fact]
        public void Is_a_digital_multimeter()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.IsAssignableFrom<IDigitalMultimeter>(driver);
        }

        [Theory]
        [InlineData(MeasurementFunction.DcVoltage, "MEAS:VOLT:DC?")]
        [InlineData(MeasurementFunction.AcVoltage, "MEAS:VOLT:AC?")]
        [InlineData(MeasurementFunction.DcCurrent, "MEAS:CURR:DC?")]
        [InlineData(MeasurementFunction.AcCurrent, "MEAS:CURR:AC?")]   // NOT the DC command (the #30 bug)
        [InlineData(MeasurementFunction.Resistance2Wire, "MEAS:RES?")]
        public void Measure_sends_the_right_query(MeasurementFunction fn, string expected)
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.SetReading(fn, 1.5);
                Assert.Equal(1.5, driver.Measure(fn), 6);
                Assert.Contains(expected, driver.History);
            }
        }

        [Fact]
        public void Ac_current_is_distinct_from_dc_current()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.SetReading(MeasurementFunction.DcCurrent, 1.0);
                sim.SetReading(MeasurementFunction.AcCurrent, 2.0);
                Assert.Equal(1.0, driver.MeasureDcCurrent(), 6);
                Assert.Equal(2.0, driver.MeasureAcCurrent(), 6);
            }
        }

        [Fact]
        public void ReadValues_repeats_the_configured_function()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.SetReading(MeasurementFunction.DcVoltage, 3.3);
                driver.Configure(MeasurementFunction.DcVoltage);
                Assert.Equal(new[] { 3.3, 3.3, 3.3 }, driver.ReadValues(3));
            }
        }

        [Fact]
        public void Initialize_resets()
        {
            var (driver, _, session) = Bench();
            using (session)
            {
                driver.Initialize();
                Assert.Equal<string>(new[] { "*RST", "*CLS" }, driver.History);
            }
        }

        [Fact]
        public void Identify_returns_idn()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.Contains("DM3058", driver.Identify());
        }
    }
}
