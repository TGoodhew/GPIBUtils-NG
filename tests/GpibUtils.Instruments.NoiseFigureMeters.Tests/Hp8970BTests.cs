using System;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.NoiseFigureMeters.Tests
{
    public class Hp8970BTests
    {
        private static IInstrumentSession Open(string reading)
        {
            var provider = new SimulatedGpibProvider();
            provider.Add(Hp8970B.DefaultResource, new SimulatedInstrument
            {
                IdentificationString = "HP8970B",
                Responder = c => c.Contains("T2") ? reading : null   // trigger-execute read
            });
            return provider.Open(Hp8970B.DefaultResource);
        }

        [Fact]
        public void Implements_noise_figure_meter()
        {
            using (var s = Open("+00350E-02"))
            {
                var d = new Hp8970B(s);
                Assert.IsAssignableFrom<INoiseFigureMeter>(d);
                Assert.Contains("8970B", d.Identify());
            }
        }

        [Fact]
        public void Initialize_holds_and_resets_status()
        {
            using (var s = Open("+00350E-02"))
            {
                var d = new Hp8970B(s);
                d.Initialize();
                Assert.Contains("T1", d.History);
                Assert.Contains("RS", d.History);
            }
        }

        [Fact]
        public void Set_start_stop_frequency_in_mhz()
        {
            using (var s = Open("+00350E-02"))
            {
                var d = new Hp8970B(s);
                d.SetStartFrequencyMHz(20);
                d.SetStopFrequencyMHz(100);
                Assert.Contains("FA20EN", d.History);
                Assert.Contains("FB100EN", d.History);
            }
        }

        [Fact]
        public void Measure_noise_figure_triggers_and_parses()
        {
            // 3.50 dB -> "+00350E-02" = 350 x 10^-2.
            using (var s = Open("+00350E-02"))
            {
                var d = new Hp8970B(s);
                d.SetFixedFrequencyMHz(45.5);
                d.SetMode(NoiseFigureMode.NoiseFigure);
                var r = d.Measure();
                Assert.Equal(3.50, r.NoiseFigureDb, 3);
                Assert.True(double.IsNaN(r.GainDb));
                Assert.Contains("FR45.5EN", d.History);
                Assert.Contains("M1", d.History);
                Assert.Contains("T2", d.History);
            }
        }

        [Fact]
        public void Nf_and_gain_mode_reads_both_values()
        {
            using (var s = Open("+00350E-02"))
            {
                var d = new Hp8970B(s);
                d.SetMode(NoiseFigureMode.NoiseFigureAndGain);
                var r = d.Measure();
                Assert.Contains("M2", d.History);
                Assert.Equal(3.50, r.NoiseFigureDb, 3);
                Assert.Equal(3.50, r.GainDb, 3);   // sim returns the same record for both reads
            }
        }

        [Fact]
        public void Error_sentinel_throws()
        {
            Assert.Throws<InvalidOperationException>(() => Hp8970B.ParseReading("+90000E+06"));
            Assert.Equal(15.20, Hp8970B.ParseReading("+01520E-02"), 3);
        }
    }
}
