using System;
using System.Linq;
using GpibUtils.Instruments.ModulationDomain;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.ModulationDomain.Tests
{
    public class Hp53310ATests
    {
        private static IInstrumentSession Open()
        {
            var provider = new SimulatedGpibProvider();
            provider.Add(Hp53310A.DefaultResource, new SimulatedInstrument
            {
                IdentificationString = "HEWLETT-PACKARD,53310A,0,1",
                Responder = c => c.Trim() == ":READ?" ? "1.000E6,1.001E6,1.002E6,1.001E6,1.000E6" : null
            });
            return provider.Open(Hp53310A.DefaultResource);
        }

        [Fact]
        public void Implements_analyzer_and_default_address()
        {
            using (var s = Open())
            {
                var d = new Hp53310A(s);
                Assert.IsAssignableFrom<IModulationDomainAnalyzer>(d);
                Assert.Equal("GPIB0::12::INSTR", Hp53310A.DefaultResource);
                Assert.Contains("53310A", d.Identify());
            }
        }

        [Fact]
        public void Configure_frequency_vs_time_channel_two()
        {
            using (var s = Open())
            {
                var d = new Hp53310A(s);
                d.Configure(ModulationMeasurement.FrequencyVsTime, 2);
                Assert.Contains(":CONFigure:XTIMe:FREQuency2", d.History);
            }
        }

        [Fact]
        public void Configure_time_interval_vs_time()
        {
            using (var s = Open())
            {
                var d = new Hp53310A(s);
                d.Configure(ModulationMeasurement.TimeIntervalVsTime);
                Assert.Contains(":CONFigure:XTIMe:TINTerval", d.History);
            }
        }

        [Fact]
        public void Read_returns_the_record_array()
        {
            using (var s = Open())
            {
                var d = new Hp53310A(s) { ReadTimeoutMs = 2000 };
                d.Configure(ModulationMeasurement.FrequencyVsTime, 1);
                var trace = d.Read();
                Assert.Equal(5, trace.Count);
                Assert.Equal(1.002e6, trace.Max(), 0);
                Assert.Contains(":READ?", d.History);
            }
        }

        [Fact]
        public void Channel_is_range_checked()
        {
            using (var s = Open())
                Assert.Throws<ArgumentOutOfRangeException>(() => new Hp53310A(s).Configure(ModulationMeasurement.FrequencyVsTime, 4));
        }
    }
}
