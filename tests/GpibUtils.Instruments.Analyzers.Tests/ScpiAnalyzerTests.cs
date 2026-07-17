using System.Linq;
using GpibUtils.Instruments.Analyzers;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.Analyzers.Tests
{
    public class ScpiAnalyzerTests
    {
        private static IInstrumentSession Open(string resource)
        {
            var provider = new SimulatedGpibProvider();
            provider.Add(resource, new SimulatedInstrument
            {
                IdentificationString = "Rigol Technologies,DSA815,0,1",
                Responder = c =>
                {
                    var t = c.Trim();
                    if (t == ":TRACe:DATA? TRACE1") return "-70,-50,-20,-50,-70";
                    if (t == ":CALCulate:MARKer1:Y?") return "-20.0";
                    if (t == ":CALCulate:MARKer1:X?") return "1.0E9";
                    return null;   // *IDN? / *OPC? handled by the base simulator
                }
            });
            return provider.Open(resource);
        }

        [Fact]
        public void Dsa800_single_sweep_trace_and_marker()
        {
            using (var s = Open(RigolDsa800.DefaultResource))
            {
                var d = new RigolDsa800(s) { SweepTimeoutMs = 2000 };
                Assert.IsAssignableFrom<ISpectrumAnalyzer>(d);
                Assert.Equal("GPIB0::18::INSTR", RigolDsa800.DefaultResource);
                d.SetCenterFrequencyHz(1e9); d.SetSpanHz(1e6); d.SingleSweep();
                Assert.Contains(":FREQuency:CENTer 1000000000 Hz", d.History);
                Assert.Contains(":INITiate:CONTinuous OFF", d.History);
                Assert.Contains("*OPC?", d.History);
                var trace = d.ReadTrace();
                Assert.Equal(5, trace.Count);
                Assert.Equal(-20, trace.Max(), 3);
                Assert.Equal(-20, d.MarkerToPeakAmplitude(), 3);
                Assert.Equal(1e9, d.MarkerFrequencyHz(), 0);
            }
        }

        [Fact]
        public void N9320a_is_a_spectrum_analyzer()
        {
            using (var s = Open(AgilentN9320A.DefaultResource))
                Assert.IsAssignableFrom<ISpectrumAnalyzer>(new AgilentN9320A(s));
        }
    }
}
