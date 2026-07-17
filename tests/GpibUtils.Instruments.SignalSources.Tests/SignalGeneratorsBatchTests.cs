using System;
using GpibUtils.Instruments.SignalSources;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.SignalSources.Tests
{
    /// <summary>Round-trip tests for the batch of ISignalSource RF generators (#103/#119/#120/#122/#123/#125/
    /// #137/#138). Each is driven against a bare simulated instrument and verified via the driver's command
    /// history (write-only) plus <c>*IDN?</c> where supported.</summary>
    public class SignalGeneratorsBatchTests
    {
        private static IInstrumentSession Open(string resource, string idn = "SIM,GEN,0,1")
        {
            var provider = new SimulatedGpibProvider();
            provider.Add(resource, new SimulatedInstrument { IdentificationString = idn });
            return provider.Open(resource);
        }

        [Fact]
        public void E4436B_scpi_carrier()
        {
            using (var s = Open(AgilentE4436B.DefaultResource))
            {
                var d = new AgilentE4436B(s);
                Assert.IsAssignableFrom<ISignalSource>(d);
                Assert.Equal("GPIB0::19::INSTR", AgilentE4436B.DefaultResource);
                d.SetFrequencyMHz(1000); d.SetPowerDbm(-10); d.RfOn();
                Assert.Contains(":FREQuency:FIXed 1000000000 Hz", d.History);
                Assert.Contains(":POWer:LEVel -10 dBm", d.History);
                Assert.Contains(":OUTPut:STATe ON", d.History);
                Assert.Contains("SIM", d.Identify());
            }
        }

        [Fact]
        public void Hp83620A_scpi_cw()
        {
            using (var s = Open(Hp83620A.DefaultResource))
            {
                var d = new Hp83620A(s);
                d.SetFrequencyMHz(4000); d.SetPowerDbm(-5); d.RfOff();
                Assert.Contains("FREQuency:CW 4000000000 HZ", d.History);
                Assert.Contains("POWer:LEVel -5 DBM", d.History);
                Assert.Contains("POWer:STATe OFF", d.History);
            }
        }

        [Fact]
        public void Hp83712B_scpi_cw()
        {
            using (var s = Open(Hp83712B.DefaultResource))
            {
                var d = new Hp83712B(s);
                d.SetFrequencyMHz(10000); d.SetPowerDbm(0); d.RfOn();
                Assert.Contains("FREQ 10000000000 HZ", d.History);
                Assert.Contains("POW 0 DBM", d.History);
                Assert.Contains("OUTP:STAT ON", d.History);
            }
        }

        [Fact]
        public void Hp8656_legacy_mnemonic_writeonly()
        {
            using (var s = Open(Hp8656.DefaultResource))
            {
                var d = new Hp8656(s);
                Assert.Equal("GPIB0::7::INSTR", Hp8656.DefaultResource);
                d.SetFrequencyMHz(123.4); d.SetPowerDbm(-10); d.RfOn(); d.RfOff();
                Assert.Contains("FR123.4MZ", d.History);
                Assert.Contains("AP-10DM", d.History);
                Assert.Contains("R1", d.History);
                Assert.Contains("R0", d.History);
                Assert.Contains("8656", d.Identify());   // descriptor, no query
            }
        }

        [Fact]
        public void Hp8657B_legacy_mnemonic_rf_codes()
        {
            using (var s = Open(Hp8657B.DefaultResource))
            {
                var d = new Hp8657B(s);
                d.SetFrequencyMHz(500); d.RfOn(); d.RfOff();
                Assert.Contains("FR500MZ", d.History);
                Assert.Contains("R3", d.History);   // RF on
                Assert.Contains("R2", d.History);   // RF off
            }
        }

        [Fact]
        public void Hp8664A_hpsl_tree()
        {
            using (var s = Open(Hp8664A.DefaultResource))
            {
                var d = new Hp8664A(s);
                d.SetFrequencyMHz(2500); d.SetPowerDbm(0); d.RfOn();
                Assert.Contains("FREQ:CW 2500MHZ", d.History);
                Assert.Contains("AMPL:LEV 0DBM", d.History);
                Assert.Contains("AMPL:STATe ON", d.History);
            }
        }

        [Fact]
        public void Rs_sme_scpi()
        {
            using (var s = Open(RohdeSchwarzSme.DefaultResource))
            {
                var d = new RohdeSchwarzSme(s);
                Assert.Equal("GPIB0::28::INSTR", RohdeSchwarzSme.DefaultResource);
                d.SetFrequencyMHz(1000); d.SetPowerDbm(-20); d.RfOn();
                Assert.Contains(":SOURce:FREQuency:CW 1000000000 Hz", d.History);
                Assert.Contains(":SOURce:POWer:LEVel -20 dBm", d.History);
                Assert.Contains(":OUTPut:STATe ON", d.History);
            }
        }

        [Fact]
        public void Rs_smt_scpi()
        {
            using (var s = Open(RohdeSchwarzSmt.DefaultResource))
            {
                var d = new RohdeSchwarzSmt(s);
                d.SetFrequencyMHz(750); d.SetPowerDbm(-30); d.RfOff();
                Assert.Contains(":SOURce:FREQuency:CW 750000000 Hz", d.History);
                Assert.Contains(":SOURce:POWer:LEVel -30 dBm", d.History);
                Assert.Contains(":OUTPut:STATe OFF", d.History);
            }
        }
    }
}
