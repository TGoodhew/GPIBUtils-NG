using System;
using GpibUtils.Instruments.Meters;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.Meters.Tests
{
    public class MeterBatchTests
    {
        private static IInstrumentSession Open(string resource, Func<string, string> responder)
        {
            var provider = new SimulatedGpibProvider();
            provider.Add(resource, new SimulatedInstrument { IdentificationString = "SIM,METER,0,1", Responder = responder });
            return provider.Open(resource);
        }

        [Fact]
        public void Keithley2015_configure_and_read()
        {
            using (var s = Open(Keithley2015.DefaultResource, c => c.Trim() == "READ?" ? "3.30012" : null))
            {
                var d = new Keithley2015(s);
                Assert.IsAssignableFrom<IDigitalMultimeter>(d);
                Assert.Equal("GPIB0::16::INSTR", Keithley2015.DefaultResource);
                d.Configure(MeasurementFunction.DcVoltage, "10");
                Assert.Contains("CONF:VOLT:DC 10", d.History);
                Assert.Equal(3.30012, d.ReadValue(), 5);
                Assert.Contains("SIM", d.Identify());
            }
        }

        [Fact]
        public void Hp437B_measures_dbm()
        {
            using (var s = Open(Hp437B.DefaultResource, c => c.Trim() == "TR2" ? "-10.50" : null))
            {
                var d = new Hp437B(s);
                Assert.IsAssignableFrom<IPowerMeter>(d);
                d.Initialize();
                Assert.Contains("PR", d.History);
                Assert.Contains("LG", d.History);
                d.ZeroAndCalibrate();
                Assert.Contains("ZE", d.History);
                Assert.Contains("CL100EN", d.History);
                Assert.Equal(-10.50, d.MeasurePowerDbm(), 2);
            }
        }

        [Fact]
        public void Hp436A_parses_fixed_output_string()
        {
            using (var s = Open(Hp436A.DefaultResource, c => c.Trim() == "9+DI" ? "PD9-1050E-02" : null))
            {
                var d = new Hp436A(s);
                Assert.IsAssignableFrom<IPowerMeter>(d);
                Assert.Contains("436A", d.Identify());
                d.ZeroAndCalibrate();
                Assert.Contains("Z", d.History);
                Assert.Equal(-10.5, d.MeasurePowerDbm(), 2);
            }
        }

        [Fact]
        public void Hp436A_parse_rejects_error_status()
        {
            Assert.Throws<InvalidOperationException>(() => Hp436A.ParseReading("QD9+1000E-02"));   // 'Q' = over-range
            Assert.Equal(-10.5, Hp436A.ParseReading("PD9-1050E-02"), 2);
        }
    }
}
