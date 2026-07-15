using System;
using System.Linq;
using GpibUtils.Instruments.Analyzers;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using GpibUtils.Visa.Srq;
using Xunit;

namespace GpibUtils.Instruments.Analyzers.Tests
{
    /// <summary>Drives the <see cref="Hp8560E"/> driver against a simulated 8560E over the standard transport,
    /// including the #43 SRQ-edge sweep-completion handshake.</summary>
    public class Hp8560ETests
    {
        private static (Hp8560E driver, Hp8560ESimulatedDevice sim, IInstrumentSession session) Bench()
        {
            var provider = new SimulatedGpibProvider();
            var sim = new Hp8560ESimulatedDevice();
            provider.Add(Hp8560E.DefaultResource, sim.Instrument);
            var session = provider.Open(Hp8560E.DefaultResource);
            // Keep completion polling brisk so the SRQ handshake tests run fast.
            var driver = new Hp8560E(session) { SweepTimeoutMs = 2000, PollIntervalMs = 5 };
            return (driver, sim, session);
        }

        [Fact]
        public void Is_a_spectrum_analyzer()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.IsAssignableFrom<ISpectrumAnalyzer>(driver);
        }

        [Fact]
        public void Default_address_is_factory_eighteen()
        {
            Assert.Equal("GPIB0::18::INSTR", Hp8560E.DefaultResource);
        }

        [Fact]
        public void Identify_uses_id_query()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.Contains("8560E", driver.Identify());
        }

        [Fact]
        public void Initialize_presets_and_clears_mask()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.Initialize();
                Assert.Contains("IP", sim.Commands);
                Assert.Contains("RQS 0", sim.Commands);
            }
        }

        [Fact]
        public void Center_frequency_and_span()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetCenterFrequencyHz(1.5e9);
                driver.SetSpanHz(1e6);
                Assert.Equal(1.5e9, sim.CenterFrequencyHz, 0);
                Assert.Equal(1e6, sim.SpanHz, 0);
                Assert.Contains("CF 1500000000 HZ", driver.History);
            }
        }

        [Fact]
        public void Center_frequency_mhz_helper()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetCenterFrequencyMHz(2400);
                Assert.Equal(2.4e9, sim.CenterFrequencyHz, 0);
            }
        }

        [Fact]
        public void Bandwidths_and_sweep_time()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetResolutionBandwidthHz(1e3);
                driver.SetVideoBandwidthHz(300);
                driver.SetSweepTimeSeconds(0.05);
                Assert.Equal(1e3, sim.ResolutionBandwidthHz, 0);
                Assert.Equal(300, sim.VideoBandwidthHz, 0);
                Assert.Equal(0.05, sim.SweepTimeSeconds, 4);
            }
        }

        [Fact]
        public void Single_sweep_completes_via_srq()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SingleSweep();   // must not throw
                // The waiter armed the RQS mask and issued SNGLS;TS; through the session.
                Assert.Contains("SNGLS", sim.Commands);
                Assert.Contains("TS", sim.Commands);
                Assert.Contains(sim.Commands, c => c.StartsWith("RQS "));
            }
        }

        [Fact]
        public void Single_sweep_throws_on_instrument_error()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.ErrorOnSweep = true;
                var ex = Assert.Throws<Hp8560EException>(() => driver.SingleSweep());
                Assert.False(ex.IsTimeout);
            }
        }

        [Fact]
        public void Single_sweep_times_out_when_sweep_never_completes()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.SweepCompletes = false;
                driver.SweepTimeoutMs = 200;   // short backstop for the test
                var ex = Assert.Throws<Hp8560EException>(() => driver.SingleSweep());
                Assert.True(ex.IsTimeout);
            }
        }

        [Fact]
        public void Read_trace_parses_points()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.Trace = new double[] { -80, -60, -20, -60, -80 };
                var trace = driver.ReadTrace();
                Assert.Equal(5, trace.Count);
                Assert.Equal(-20, trace.Max(), 3);
            }
        }

        [Fact]
        public void Marker_to_peak_returns_trace_max()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.Trace = new double[] { -70, -40, -12, -40, -70 };
                Assert.Equal(-12, driver.MarkerToPeakAmplitude(), 3);
                Assert.Contains("MKPK HI", driver.History);
            }
        }

        [Fact]
        public void Marker_frequency_query()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.MarkerFrequencyHz = 1.2345e9;
                Assert.Equal(1.2345e9, driver.MarkerFrequencyHz(), 0);
            }
        }

        [Fact]
        public void Status_model_is_srq_edge_shaped()
        {
            var model = Hp8560E.StatusModel();
            Assert.True(model.SrqSupported);
            Assert.Equal("requestService", model.RequestServiceBit);   // => SRQ-edge flow
            Assert.Equal("error", model.ErrorBit);
            Assert.Equal(64, model.BitValue("requestService"));
            Assert.Equal(16, model.BitValue("commandComplete"));
            Assert.Equal("RQS {mask}", model.EnableMask.SetCommand);
            Assert.True(model.Operations.ContainsKey("sweepComplete"));
            Assert.Equal("commandComplete", model.Operations["sweepComplete"].ExpectBit);
        }

        [Fact]
        public void Parse_trace_rejects_empty()
        {
            Assert.Throws<FormatException>(() => Hp8560E.ParseTrace(""));
        }
    }
}
