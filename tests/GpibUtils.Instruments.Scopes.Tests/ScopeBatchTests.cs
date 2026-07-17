using System;
using GpibUtils.Instruments.Scopes;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.Scopes.Tests
{
    /// <summary>Round-trip tests for the scope batch (#100/#101/#139 Tek, #115/#116 Agilent, #135/#140 LeCroy),
    /// driven against a simulated instrument whose responder returns the measurement query.</summary>
    public class ScopeBatchTests
    {
        private static IInstrumentSession Open(string resource, Func<string, string> responder = null)
        {
            var provider = new SimulatedGpibProvider();
            provider.Add(resource, new SimulatedInstrument { IdentificationString = "SIM,SCOPE,0,1", Responder = responder });
            return provider.Open(resource);
        }

        [Fact]
        public void Tektronix_dpo3000_control_and_measure()
        {
            using (var s = Open(TektronixDpo3000.DefaultResource, c => c.Trim() == "MEASUrement:IMMed:VALue?" ? "1.234" : null))
            {
                var d = new TektronixDpo3000(s);
                Assert.IsAssignableFrom<IOscilloscope>(d);
                d.Run(); d.Single(); d.AutoScale(); d.SetChannelDisplay(2, true);
                Assert.Contains("ACQuire:STATE RUN", d.History);
                Assert.Contains("ACQuire:STOPAfter SEQuence", d.History);
                Assert.Contains("AUTOSet EXECute", d.History);
                Assert.Contains("SELect:CH2 ON", d.History);
                Assert.Equal(1.234, d.MeasureVpp(1), 3);
                Assert.Contains("MEASUrement:IMMed:TYPe PK2Pk", d.History);
            }
        }

        [Fact]
        public void Tektronix_siblings_are_scopes()
        {
            using (var s = Open(TektronixDpo4000.DefaultResource)) Assert.IsAssignableFrom<IOscilloscope>(new TektronixDpo4000(s));
            using (var s = Open(TektronixTds784.DefaultResource)) Assert.IsAssignableFrom<IOscilloscope>(new TektronixTds784(s));
        }

        [Fact]
        public void Agilent_54622_control_and_measure()
        {
            using (var s = Open(Hp54622A.DefaultResource, c => c.Trim().StartsWith(":MEASure:VPP?") ? "2.5" : null))
            {
                var d = new Hp54622A(s);
                Assert.Equal("GPIB0::7::INSTR", Hp54622A.DefaultResource);
                d.Run(); d.Stop(); d.AutoScale(); d.SetChannelDisplay(2, false);
                Assert.Contains(":RUN", d.History);
                Assert.Contains(":STOP", d.History);
                Assert.Contains(":AUToscale", d.History);
                Assert.Contains(":CHANnel2:DISPlay OFF", d.History);
                Assert.Equal(2.5, d.MeasureVpp(1), 3);
                Assert.Contains(":MEASure:VPP? CHANnel1", d.History);
                Assert.Throws<ArgumentOutOfRangeException>(() => d.SetChannelDisplay(3, true));   // 2-channel
            }
        }

        [Fact]
        public void Agilent_54845_is_a_scope()
        {
            using (var s = Open(Hp54845A.DefaultResource)) Assert.IsAssignableFrom<IOscilloscope>(new Hp54845A(s));
        }

        [Fact]
        public void LeCroy_lc574a_control_and_measure()
        {
            using (var s = Open(LeCroyLC574A.DefaultResource, c => c.Contains("PAVA?") ? "C1:PAVA PKPK,4.960E-01,V" : null))
            {
                var d = new LeCroyLC574A(s);
                Assert.IsAssignableFrom<IOscilloscope>(d);
                d.Run(); d.Single(); d.AutoScale(); d.SetChannelDisplay(1, true);
                Assert.Contains("TRMD AUTO", d.History);
                Assert.Contains("TRMD SINGLE", d.History);
                Assert.Contains("ASET", d.History);
                Assert.Contains("C1:TRA ON", d.History);
                Assert.Equal(0.496, d.MeasureVpp(1), 3);
            }
        }

        [Fact]
        public void LeCroy_waverunner_is_a_scope()
        {
            using (var s = Open(LeCroyWaveRunner6000.DefaultResource)) Assert.IsAssignableFrom<IOscilloscope>(new LeCroyWaveRunner6000(s));
        }

        [Fact]
        public void LeCroy_parse_handles_bare_number_and_pava_reply()
        {
            Assert.Equal(0.496, LeCroyScope.ParseReading("C1:PAVA PKPK,4.960E-01,V"), 3);
            Assert.Equal(1.5, LeCroyScope.ParseReading("1.5"), 3);
        }
    }
}
