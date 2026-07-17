using System;
using System.Linq;
using GpibUtils.Instruments.Analyzers;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.Analyzers.Tests
{
    /// <summary>Drives the <see cref="Hp3585"/> driver against a simulated 3585B over the standard transport,
    /// including the legacy <c>CQ</c>-enabled operation-complete SRQ sweep-completion handshake (a custom
    /// non-<c>RQS</c> enable command through the shared #43/#96 engine).</summary>
    public class Hp3585Tests
    {
        private static (Hp3585 driver, Hp3585SimulatedDevice sim, IInstrumentSession session) Bench()
        {
            var provider = new SimulatedGpibProvider();
            var sim = new Hp3585SimulatedDevice();
            provider.Add(Hp3585.DefaultResource, sim.Instrument);
            var session = provider.Open(Hp3585.DefaultResource);
            var driver = new Hp3585(session) { SweepTimeoutMs = 2000, PollIntervalMs = 5 };
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
        public void Default_address_is_factory_eleven()
        {
            Assert.Equal("GPIB0::11::INSTR", Hp3585.DefaultResource);
        }

        [Fact]
        public void Identify_returns_descriptor_without_query()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.Contains("3585", driver.Identify());
        }

        [Fact]
        public void Initialize_presets_and_disables_op_complete_srq()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.Initialize();
                Assert.Contains("PR", sim.Commands);
                Assert.Contains("CC", sim.Commands);
            }
        }

        [Fact]
        public void Center_frequency_and_span()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetCenterFrequencyHz(10e6);
                driver.SetSpanHz(100e3);
                Assert.Equal(10e6, sim.CenterFrequencyHz, 0);
                Assert.Equal(100e3, sim.SpanHz, 0);
                Assert.Contains("CF 10000000 HZ", driver.History);
            }
        }

        [Fact]
        public void Single_sweep_completes_via_cq_op_complete_srq()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SingleSweep();   // must not throw
                Assert.Contains("CQ", sim.Commands);   // op-complete SRQ enabled (custom enable command)
                Assert.Contains("S2", sim.Commands);   // single sweep
                Assert.Contains("T5", sim.Commands);   // delayed trigger
            }
        }

        [Fact]
        public void Single_sweep_throws_on_instrument_error()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.ErrorOnSweep = true;
                var ex = Assert.Throws<Hp3585Exception>(() => driver.SingleSweep());
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
                driver.SweepTimeoutMs = 200;
                var ex = Assert.Throws<Hp3585Exception>(() => driver.SingleSweep());
                Assert.True(ex.IsTimeout);
            }
        }

        [Fact]
        public void Read_trace_parses_d3_dump()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.Trace = new double[] { -90, -60, -18, -60, -90 };
                var trace = driver.ReadTrace();
                Assert.Equal(5, trace.Count);
                Assert.Equal(-18, trace.Max(), 3);
            }
        }

        [Fact]
        public void Marker_to_peak_is_trace_max()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.Trace = new double[] { -80, -50, -15, -50, -80 };
                Assert.Equal(-15, driver.MarkerToPeakAmplitude(), 3);
            }
        }

        [Fact]
        public void Marker_frequency_from_d2_dump()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.MarkerFrequencyHz = 1.234e7;
                Assert.Equal(1.234e7, driver.MarkerFrequencyHz(), 0);
            }
        }

        [Fact]
        public void Status_model_uses_cq_enable_and_op_complete()
        {
            var model = Hp3585.StatusModel();
            Assert.True(model.SrqSupported);
            Assert.Null(model.StatusQuery);                          // hardware serial poll, not STB? query
            Assert.Equal("CQ", model.EnableMask.SetCommand);         // custom (non-RQS) enable command
            Assert.Equal("CC", model.EnableMask.ClearCommand);
            Assert.Equal("requestService", model.RequestServiceBit);
            Assert.Equal(8, model.BitValue("operationComplete"));
            Assert.Equal("operationComplete", model.Operations["sweepComplete"].ExpectBit);
            Assert.Equal("S2;T5;", model.Operations["sweepComplete"].Arm);
        }
    }
}
