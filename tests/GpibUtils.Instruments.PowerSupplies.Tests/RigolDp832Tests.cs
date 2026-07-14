using System;
using GpibUtils.Instruments.PowerSupplies;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.PowerSupplies.Tests
{
    /// <summary>Drives the <see cref="RigolDp832"/> driver against a simulated DP832 over the standard transport.</summary>
    public class RigolDp832Tests
    {
        private static (RigolDp832 driver, RigolDp832SimulatedDevice sim, IInstrumentSession session) Bench()
        {
            var provider = new SimulatedGpibProvider();
            var sim = new RigolDp832SimulatedDevice();
            provider.Add(RigolDp832.DefaultResource, sim.Instrument);
            var session = provider.Open(RigolDp832.DefaultResource);
            return (new RigolDp832(session), sim, session);
        }

        [Fact]
        public void Default_resource_is_gpib_2()
        {
            Assert.Equal("GPIB0::2::INSTR", RigolDp832.DefaultResource);
        }

        [Fact]
        public void Is_a_dc_power_supply()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.IsAssignableFrom<IDcPowerSupply>(driver);
        }

        [Fact]
        public void Per_channel_set_uses_source_keyword()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetVoltage(2, 12.0);
                driver.SetCurrentLimit(2, 1.5);
                driver.SetOutput(2, true);
                Assert.Equal(":SOUR2:VOLT 12.000", driver.History[0]);
                Assert.Equal(":SOUR2:CURR 1.500", driver.History[1]);
                Assert.Equal(":OUTP CH2,ON", driver.History[2]);
                Assert.Equal(12.0, sim.Voltage(2), 3);
                Assert.True(sim.OutputOn(2));
            }
        }

        [Fact]
        public void IDcPowerSupply_members_act_on_selected_channel()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SelectedChannel = 3;
                driver.SetVoltage(4.0);
                Assert.Equal(":SOUR3:VOLT 4.000", Assert.Single(driver.History));
                Assert.Equal(4.0, sim.Voltage(3), 3);
            }
        }

        [Fact]
        public void Measure_reflects_output_state_and_load()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetVoltage(1, 10.0);
                sim.SetLoadCurrent(1, 0.5);
                Assert.Equal(0.0, driver.MeasureVoltage(1), 3);   // off
                driver.SetOutput(1, true);
                Assert.Equal(10.0, driver.MeasureVoltage(1), 3);
                Assert.Equal(0.5, driver.MeasureCurrent(1), 3);
                Assert.Equal(5.0, driver.MeasurePower(1), 3);
                Assert.True(driver.IsOutputOn(1));
            }
        }

        [Fact]
        public void Protection_is_decoded()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetOverVoltageProtection(1, 33.0);
                driver.SetOverVoltageProtectionEnabled(1, true);
                driver.SetOverCurrentProtection(1, 3.2);
                driver.SetOverCurrentProtectionEnabled(1, true);
                Assert.Equal(33.0, sim.Ovp(1), 3);
                Assert.True(sim.OvpEnabled(1));
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(4)]
        public void Out_of_range_channel_throws(int channel)
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.Throws<ArgumentOutOfRangeException>(() => driver.SetVoltage(channel, 1.0));
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
        public void ParseBoolean_reads_on_off()
        {
            Assert.True(RigolDp832.ParseBoolean("ON"));
            Assert.True(RigolDp832.ParseBoolean("1"));
            Assert.False(RigolDp832.ParseBoolean("OFF"));
        }
    }
}
