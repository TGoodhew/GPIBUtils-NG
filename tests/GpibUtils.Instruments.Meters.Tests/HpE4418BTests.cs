using System;
using GpibUtils.Instruments.Meters;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.Meters.Tests
{
    /// <summary>Drives the <see cref="HpE4418B"/> driver (incl. its #43 OPC→SRQ completion) against a simulated E4418B.</summary>
    public class HpE4418BTests
    {
        private static (HpE4418B driver, HpE4418BSimulatedDevice sim, IInstrumentSession session) Bench()
        {
            var provider = new SimulatedGpibProvider();
            var sim = new HpE4418BSimulatedDevice();
            provider.Add(HpE4418B.DefaultResource, sim.Instrument);
            var session = provider.Open(HpE4418B.DefaultResource);
            return (new HpE4418B(session) { PollIntervalMs = 5 }, sim, session);
        }

        [Fact]
        public void Is_a_power_meter()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.IsAssignableFrom<IPowerMeter>(driver);
        }

        [Fact]
        public void Initialize_resets_and_disables_srq()
        {
            var (driver, _, session) = Bench();
            using (session)
            {
                driver.Initialize();
                Assert.Equal<string>(new[] { "*RST", "*CLS", "*SRE 0", "*ESE 0" }, driver.History);
            }
        }

        [Fact]
        public void SetFrequency_sends_MHZ_and_is_decoded()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetFrequencyMHz(1000);
                Assert.Equal(":FREQ 1000MHZ", Assert.Single(driver.History));
                Assert.Equal(1000, sim.FrequencyMHz);
            }
        }

        [Fact]
        public void MeasurePower_completes_via_srq_and_returns_dbm()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.PowerDbm = -12.34;
                var dbm = driver.MeasurePowerDbm();
                Assert.Equal(-12.34, dbm, 4);
                Assert.Contains("*SRE 32", sim.Commands);                 // enable mask armed
                Assert.Contains("*ESE 1;:CONF1;:INIT;*OPC", sim.Commands); // measure arm
                Assert.Contains("FETCH?", driver.History);
            }
        }

        [Fact]
        public void ZeroAndCalibrate_completes_via_srq()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.ZeroAndCalibrate();
                Assert.Contains("*ESE 1;:CAL1:ALL;*OPC", sim.Commands);
            }
        }

        [Fact]
        public void StatusModel_uses_SRE_mask_and_ESB_bit()
        {
            var m = HpE4418B.StatusModel();
            Assert.Equal("*SRE {mask}", m.EnableMask.SetCommand);
            Assert.Equal(0x20, m.BitValue("operationComplete"));
            Assert.Null(m.RequestServiceBit);
        }

        [Theory]
        [InlineData("-1.23400000E+01", -12.34)]
        [InlineData("0", 0.0)]
        public void ParsePower_parses_scientific(string raw, double expected)
        {
            Assert.Equal(expected, HpE4418B.ParsePower(raw), 4);
        }
    }
}
