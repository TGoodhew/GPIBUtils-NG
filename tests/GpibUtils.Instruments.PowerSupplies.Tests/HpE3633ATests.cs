using System;
using GpibUtils.Instruments.PowerSupplies;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.PowerSupplies.Tests
{
    /// <summary>Drives the <see cref="HpE3633A"/> driver against a simulated E3633A over the standard transport.</summary>
    public class HpE3633ATests
    {
        private static (HpE3633A driver, HpE3633ASimulatedDevice sim, IInstrumentSession session) Bench()
        {
            var provider = new SimulatedGpibProvider();
            var sim = new HpE3633ASimulatedDevice();
            provider.Add(HpE3633A.DefaultResource, sim.Instrument);
            var session = provider.Open(HpE3633A.DefaultResource);
            return (new HpE3633A(session), sim, session);
        }

        [Fact]
        public void Default_resource_is_factory_gpib_5()
        {
            Assert.Equal("GPIB0::5::INSTR", HpE3633A.DefaultResource);
        }

        [Fact]
        public void Is_a_dc_power_supply()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.IsAssignableFrom<IDcPowerSupply>(driver);
        }

        [Fact]
        public void Initialize_clears_then_resets()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.Initialize();
                Assert.Equal<string>(new[] { "*RST", "*CLS" }, driver.History);
            }
        }

        [Fact]
        public void Set_voltage_current_and_output_are_decoded()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetVoltage(12.5);
                driver.SetCurrentLimit(1.25);
                driver.SetOutput(true);
                Assert.Equal("VOLT 12.5", driver.History[0]);
                Assert.Equal("CURR 1.25", driver.History[1]);
                Assert.Equal("OUTP ON", driver.History[2]);
                Assert.Equal(12.5, sim.Voltage, 6);
                Assert.Equal(1.25, sim.CurrentLimit, 6);
                Assert.True(sim.OutputOn);
            }
        }

        [Fact]
        public void Measure_voltage_reads_zero_when_output_off_and_setpoint_when_on()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetVoltage(10);
                Assert.Equal(0.0, driver.MeasureVoltage(), 6);   // output off
                driver.SetOutput(true);
                Assert.Equal(10.0, driver.MeasureVoltage(), 6);
            }
        }

        [Fact]
        public void Measure_current_reflects_the_load_when_on()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.LoadCurrent = 0.5;
                driver.SetOutput(true);
                Assert.Equal(0.5, driver.MeasureCurrent(), 6);
            }
        }

        [Fact]
        public void Over_voltage_protection_is_decoded()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetOverVoltageProtection(15);
                driver.SetOverVoltageProtectionEnabled(true);
                Assert.Equal(15.0, sim.OverVoltageProtection, 6);
                Assert.True(sim.OverVoltageProtectionEnabled);
            }
        }

        [Fact]
        public void Identify_returns_idn()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.Contains("E3633A", driver.Identify());
        }

        [Theory]
        [InlineData("+1.200000E+01", 12.0)]
        [InlineData("0", 0.0)]
        public void ParseReading_parses_scientific(string raw, double expected)
        {
            Assert.Equal(expected, HpE3633A.ParseReading(raw), 6);
        }

        [Fact]
        public void ParseReading_rejects_garbage()
        {
            Assert.Throws<FormatException>(() => HpE3633A.ParseReading("x"));
        }
    }
}
