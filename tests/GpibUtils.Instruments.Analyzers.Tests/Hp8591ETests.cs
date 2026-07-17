using System;
using System.Linq;
using GpibUtils.Instruments.Analyzers;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.Analyzers.Tests
{
    /// <summary>Drives the <see cref="Hp8591E"/> driver against a simulated 8591E over the standard transport,
    /// including the legacy pre-488.2 RQS-mask / <c>STB?</c> sweep-completion handshake (the #96 status-via-query
    /// path).</summary>
    public class Hp8591ETests
    {
        private static (Hp8591E driver, Hp8591ESimulatedDevice sim, IInstrumentSession session) Bench()
        {
            var provider = new SimulatedGpibProvider();
            var sim = new Hp8591ESimulatedDevice();
            provider.Add(Hp8591E.DefaultResource, sim.Instrument);
            var session = provider.Open(Hp8591E.DefaultResource);
            var driver = new Hp8591E(session) { SweepTimeoutMs = 2000, PollIntervalMs = 5 };
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
        public void Default_address_is_family_eighteen()
        {
            Assert.Equal("GPIB0::18::INSTR", Hp8591E.DefaultResource);
        }

        [Fact]
        public void Identify_uses_id_query()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.Contains("8591E", driver.Identify());
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
        public void Center_frequency_and_span_use_fixed_notation()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetCenterFrequencyHz(300e6);
                driver.SetSpanHz(20e6);
                Assert.Equal(300e6, sim.CenterFrequencyHz, 0);
                Assert.Equal(20e6, sim.SpanHz, 0);
                Assert.Contains("CF 300000000 HZ", driver.History);
            }
        }

        [Fact]
        public void Bandwidths_and_sweep_time()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetResolutionBandwidthHz(3e6);
                driver.SetVideoBandwidthHz(1e4);
                driver.SetSweepTimeSeconds(0.1);
                Assert.Equal(3e6, sim.ResolutionBandwidthHz, 0);
                Assert.Equal(1e4, sim.VideoBandwidthHz, 0);
                Assert.Equal(0.1, sim.SweepTimeSeconds, 4);
            }
        }

        [Fact]
        public void Single_sweep_completes_via_stb_query_path()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SingleSweep();   // must not throw
                // The waiter armed the RQS mask, issued SNGLS;TS;, and polled the status byte via STB?.
                Assert.Contains("SNGLS", sim.Commands);
                Assert.Contains("TS", sim.Commands);
                Assert.Contains(sim.Commands, c => c.StartsWith("RQS "));
                Assert.Contains("STB?", sim.Commands);   // status read via query, not serial poll
            }
        }

        [Fact]
        public void Single_sweep_throws_on_instrument_error()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.ErrorOnSweep = true;
                var ex = Assert.Throws<Hp8591EException>(() => driver.SingleSweep());
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
                var ex = Assert.Throws<Hp8591EException>(() => driver.SingleSweep());
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
                sim.MarkerFrequencyHz = 3.21e8;
                Assert.Equal(3.21e8, driver.MarkerFrequencyHz(), 0);
            }
        }

        [Fact]
        public void Status_model_reads_status_via_stb_query()
        {
            var model = Hp8591E.StatusModel();
            Assert.True(model.SrqSupported);
            Assert.NotNull(model.StatusQuery);
            Assert.Equal("STB?", model.StatusQuery.Command);          // #96 status-via-query
            Assert.Equal("requestService", model.RequestServiceBit);  // => SRQ-edge flow
            Assert.Equal(64, model.BitValue("requestService"));
            Assert.Equal(4, model.BitValue("endOfSweep"));
            Assert.Equal("RQS {mask}", model.EnableMask.SetCommand);
            Assert.Equal("endOfSweep", model.Operations["sweepComplete"].ExpectBit);
        }
    }
}
