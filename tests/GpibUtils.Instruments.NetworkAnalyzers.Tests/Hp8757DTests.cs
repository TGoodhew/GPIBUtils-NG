using System;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.NetworkAnalyzers.Tests
{
    public class Hp8757DTests
    {
        private static IInstrumentSession Open(string traceReading = null)
        {
            var provider = new SimulatedGpibProvider();
            provider.Add(Hp8757D.DefaultResource, new SimulatedInstrument
            {
                IdentificationString = "HP8757D",
                Responder = c =>
                {
                    if (c.Contains("OI")) return "HP8757D";
                    if (c.Contains("OD")) return traceReading;
                    return null;
                }
            });
            return provider.Open(Hp8757D.DefaultResource);
        }

        [Fact]
        public void Implements_network_analyzer_and_default_address_16()
        {
            using (var s = Open())
            {
                var d = new Hp8757D(s);
                Assert.IsAssignableFrom<INetworkAnalyzer>(d);
                Assert.Equal("GPIB0::16::INSTR", Hp8757D.DefaultResource);
                Assert.Equal("HP8757D", d.Identify());
            }
        }

        [Fact]
        public void Preset_and_measurement_select_use_scalar_codes()
        {
            using (var s = Open())
            {
                var d = new Hp8757D(s);
                d.Initialize();
                d.SetMeasurement(NetworkParameter.S21);   // transmission -> detector B
                d.SetMeasurement(NetworkParameter.S11);   // reflection -> detector A
                d.SetMeasurement(NetworkParameter.InputR);
                Assert.Contains("IP", d.History);
                Assert.Contains("IB", d.History);
                Assert.Contains("IA", d.History);
                Assert.Contains("IR", d.History);
            }
        }

        [Fact]
        public void Reverse_parameters_are_unsupported_on_a_scalar_analyzer()
        {
            using (var s = Open())
            {
                var d = new Hp8757D(s);
                Assert.Throws<NotSupportedException>(() => d.SetMeasurement(NetworkParameter.S12));
                Assert.Throws<NotSupportedException>(() => d.SetMeasurement(NetworkParameter.S22));
            }
        }

        [Fact]
        public void Source_power_is_a_documented_no_op()
        {
            using (var s = Open())
            {
                var d = new Hp8757D(s);
                d.SetSourcePowerDbm(-10);
                // No program code is sent; a visibility note is recorded instead.
                Assert.DoesNotContain(d.History, h => h.StartsWith("PL") || h.StartsWith("POWE") || h.StartsWith("AP"));
                Assert.Contains(d.History, h => h.Contains("ignored"));
            }
        }

        [Fact]
        public void Sweep_points_validated()
        {
            using (var s = Open())
            {
                var d = new Hp8757D(s);
                d.SetSweepPoints(401);
                Assert.Contains("SP401", d.History);
                Assert.Throws<ArgumentOutOfRangeException>(() => d.SetSweepPoints(500));
            }
        }

        [Fact]
        public void Single_sweep_then_peak_marker_computed_host_side()
        {
            // Trace peak (3.0) is at index 2 of 5 points; with 101..? use points=5-equivalent scaling.
            using (var s = Open("-10.0,-5.0,3.0,-2.0,-8.0"))
            {
                var d = new Hp8757D(s);
                d.SetStartFrequencyHz(1e9);
                d.SetStopFrequencyHz(2e9);
                d.SetSweepPoints(401);
                d.SingleSweep();
                Assert.Contains("CS", d.History);
                Assert.Contains("SV1", d.History);

                var trace = d.ReadFormattedTrace();
                Assert.Equal(5, trace.Count);
                Assert.Equal(3.0, d.MarkerToPeakY(), 3);
                // peak index 2 of (points-1)=400 -> start + 2/400 * span
                Assert.Equal(1e9 + (2.0 / 400.0) * 1e9, d.MarkerFrequencyHz(), 0);
            }
        }

        [Fact]
        public void Marker_frequency_requires_a_peak_first()
        {
            using (var s = Open("-10.0,-5.0"))
            {
                var d = new Hp8757D(s);
                Assert.Throws<InvalidOperationException>(() => d.MarkerFrequencyHz());
            }
        }
    }
}
