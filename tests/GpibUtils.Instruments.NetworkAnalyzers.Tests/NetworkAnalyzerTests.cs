using System;
using System.Linq;
using GpibUtils.Instruments.NetworkAnalyzers;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.NetworkAnalyzers.Tests
{
    public class NetworkAnalyzerTests
    {
        private static IInstrumentSession Open(string resource, Func<string, string> responder)
        {
            var provider = new SimulatedGpibProvider();
            provider.Add(resource, new SimulatedInstrument { IdentificationString = "HEWLETT PACKARD,8714C,0,1", Responder = responder });
            return provider.Open(resource);
        }

        [Fact]
        public void Hp8714_scpi_sweep_trace_marker()
        {
            using (var s = Open(Hp8714.DefaultResource, c =>
            {
                var t = c.Trim();
                if (t == "TRACe:DATA? CH1FDATA") return "-1.0,-2.0,-20.0,-2.0,-1.0";
                if (t == "CALCulate1:MARKer1:Y?") return "-1.0";
                if (t == "CALCulate1:MARKer1:X?") return "1.5E9";
                return null;   // *IDN? / *OPC? via base
            }))
            {
                var d = new Hp8714(s) { SweepTimeoutMs = 2000 };
                Assert.IsAssignableFrom<INetworkAnalyzer>(d);
                Assert.Equal("GPIB0::16::INSTR", Hp8714.DefaultResource);
                d.SetStartFrequencyHz(10e6); d.SetStopFrequencyHz(400e6); d.SetSourcePowerDbm(0);
                d.SetMeasurement(NetworkParameter.S21);
                d.SingleSweep();
                Assert.Contains("SENSe1:FREQuency:STARt 10000000", d.History);
                Assert.Contains("SENSe1:FUNCtion 'XFR:POW:RAT 2,0'", d.History);
                Assert.Contains("*OPC?", d.History);
                var trace = d.ReadFormattedTrace();
                Assert.Equal(5, trace.Count);
                Assert.Equal(-1.0, trace.Max(), 3);
                Assert.Equal(-1.0, d.MarkerToPeakY(), 3);
                Assert.Equal(1.5e9, d.MarkerFrequencyHz(), 0);
            }
        }

        [Fact]
        public void Hp8714_rejects_full_2port_params()
        {
            using (var s = Open(Hp8714.DefaultResource, _ => null))
                Assert.Throws<NotSupportedException>(() => new Hp8714(s).SetMeasurement(NetworkParameter.S22));
        }

        [Fact]
        public void Hp8720c_mnemonic_sweep_trace_marker()
        {
            using (var s = Open(Hp8720C.DefaultResource, c =>
            {
                var t = c.Trim();
                if (t == "OUTPFORM") return "-1,-2,-20,-2,-1";
                if (t == "OUTPMARK") return "-1.50,0,10.5E9";
                return null;
            }))
            {
                var d = new Hp8720C(s) { SweepTimeoutMs = 2000 };
                Assert.IsAssignableFrom<INetworkAnalyzer>(d);
                d.SetStartFrequencyHz(50e6); d.SetMeasurement(NetworkParameter.S11); d.SingleSweep();
                Assert.Contains("STAR 50000000 HZ", d.History);
                Assert.Contains("S11", d.History);
                Assert.Contains("SING", d.History);
                Assert.Contains("*OPC?", d.History);
                Assert.Equal(5, d.ReadFormattedTrace().Count);
                Assert.Equal(-1.50, d.MarkerToPeakY(), 3);
                Assert.Equal(10.5e9, d.MarkerFrequencyHz(), 0);
            }
        }
    }
}
