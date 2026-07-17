using System;
using GpibUtils.Instruments.Meters;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.Meters.Tests
{
    public class Hp8901Tests
    {
        private static IInstrumentSession Open(string reading)
        {
            var provider = new SimulatedGpibProvider();
            provider.Add(Hp8901.DefaultResource, new SimulatedInstrument
            {
                IdentificationString = "HP8901",
                Responder = c => c.Contains("T3") ? reading : null   // trigger-with-settling read
            });
            return provider.Open(Hp8901.DefaultResource);
        }

        [Fact]
        public void Implements_modulation_analyzer()
        {
            using (var s = Open("+0000010000E+00"))
            {
                var d = new Hp8901(s);
                Assert.IsAssignableFrom<IModulationAnalyzer>(d);
                Assert.Contains("8901", d.Identify());
            }
        }

        [Fact]
        public void Preset_and_tune()
        {
            using (var s = Open("+0000010000E+00"))
            {
                var d = new Hp8901(s);
                d.Initialize();
                d.TuneMHz(104.5);
                Assert.Contains("IP", d.History);
                Assert.Contains("AU 104.5 MZ", d.History);
            }
        }

        [Fact]
        public void Measure_fm_triggers_and_parses_exponential()
        {
            // 969.21346 MHz -> "+0096921346E+01" per the manual (parses directly as a float).
            using (var s = Open("+0096921346E+01"))
            {
                var d = new Hp8901(s);
                Assert.Equal(969213460.0, d.Measure(ModulationMeasurementType.Fm), 0);
                Assert.Contains("M2 T3", d.History);
            }
        }

        [Fact]
        public void Measure_am_uses_m1()
        {
            using (var s = Open("+0000012500E-02"))
            {
                var d = new Hp8901(s);
                Assert.Equal(125.0, d.Measure(ModulationMeasurementType.Am), 3);
                Assert.Contains("M1 T3", d.History);
            }
        }

        [Fact]
        public void Error_sentinel_throws()
        {
            Assert.Throws<InvalidOperationException>(() => Hp8901.ParseReading("+9000000000E+01"));
            Assert.Equal(1000.0, Hp8901.ParseReading("+0000010000E-01"), 3);
        }
    }
}
