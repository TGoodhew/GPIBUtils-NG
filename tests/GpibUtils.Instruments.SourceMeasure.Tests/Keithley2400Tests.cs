using System;
using GpibUtils.Instruments.SourceMeasure;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.SourceMeasure.Tests
{
    public class Keithley2400Tests
    {
        private static IInstrumentSession Open()
        {
            var provider = new SimulatedGpibProvider();
            provider.Add(Keithley2400.DefaultResource, new SimulatedInstrument
            {
                IdentificationString = "KEITHLEY INSTRUMENTS INC.,MODEL 2400,0,1",
                Responder = c => c.Trim() == ":READ?" ? "1.000000E+00,5.000000E-03,2.000000E+02" : null
            });
            return provider.Open(Keithley2400.DefaultResource);
        }

        [Fact]
        public void Implements_smu_and_default_address()
        {
            using (var s = Open())
            {
                var d = new Keithley2400(s);
                Assert.IsAssignableFrom<ISourceMeasureUnit>(d);
                Assert.Equal("GPIB0::24::INSTR", Keithley2400.DefaultResource);
                Assert.Contains("2400", d.Identify());
            }
        }

        [Fact]
        public void Source_voltage_with_current_compliance_then_measure()
        {
            using (var s = Open())
            {
                var d = new Keithley2400(s);
                d.SetSourceFunction(SmuSourceFunction.Voltage);
                d.SetSourceLevel(1.0);
                d.SetCompliance(0.01);   // current compliance when sourcing V
                d.SetOutput(true);
                Assert.Contains(":SOURce:FUNCtion VOLTage", d.History);
                Assert.Contains(":SOURce:VOLTage:LEVel 1", d.History);
                Assert.Contains(":SENSe:CURRent:PROTection 0.01", d.History);
                Assert.Contains(":OUTPut ON", d.History);

                var r = d.Measure();
                Assert.Equal(1.0, r.Voltage, 3);
                Assert.Equal(0.005, r.Current, 4);
                Assert.Equal(200.0, r.Resistance, 1);
            }
        }

        [Fact]
        public void Source_current_uses_voltage_compliance()
        {
            using (var s = Open())
            {
                var d = new Keithley2400(s);
                d.SetSourceFunction(SmuSourceFunction.Current);
                d.SetSourceLevel(0.001);
                d.SetCompliance(20);   // voltage compliance when sourcing I
                Assert.Contains(":SOURce:CURRent:LEVel 0.001", d.History);
                Assert.Contains(":SENSe:VOLTage:PROTection 20", d.History);
            }
        }

        [Fact]
        public void Parse_reading_requires_three_fields()
        {
            Assert.Throws<FormatException>(() => Keithley2400.ParseReading("1.0,2.0"));
            var r = Keithley2400.ParseReading("1.0,2.0,3.0");
            Assert.Equal(3.0, r.Resistance, 3);
        }
    }
}
