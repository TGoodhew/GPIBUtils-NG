using System;
using GpibUtils.Instruments.Counters;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.Counters.Tests
{
    /// <summary>
    /// Drives the <see cref="Hp53131A"/> driver against a simulated 53131A over the standard transport,
    /// exercising the real configure / #43-completion / FETCH? path with no hardware.
    /// </summary>
    public class Hp53131ATests
    {
        private static (Hp53131A driver, Hp53131ASimulatedDevice sim, IInstrumentSession session) Bench()
        {
            var provider = new SimulatedGpibProvider();
            var sim = new Hp53131ASimulatedDevice();
            provider.Add(Hp53131A.DefaultResource, sim.Instrument);
            var session = provider.Open(Hp53131A.DefaultResource);
            // Small poll interval keeps the (few) timing-dependent tests quick.
            var driver = new Hp53131A(session) { PollIntervalMs = 5 };
            return (driver, sim, session);
        }

        [Fact]
        public void Default_resource_is_factory_gpib_3()
        {
            Assert.Equal("GPIB0::3::INSTR", Hp53131A.DefaultResource);
        }

        [Fact]
        public void Is_a_frequency_counter()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.IsAssignableFrom<IFrequencyCounter>(driver);
        }

        [Fact]
        public void Initialize_resets_and_presets_status()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.Initialize();
                Assert.Equal<string>(new[] { "*RST", "*CLS", "*SRE 0", "*ESE 0", ":STAT:PRES" }, driver.History);
                Assert.Contains(":STAT:PRES", sim.Commands);
            }
        }

        [Fact]
        public void Identify_returns_the_idn_string()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.Contains("53131A", driver.Identify());
        }

        [Theory]
        [InlineData(CounterInputImpedance.Ohms50, "INP:IMP 50", true)]
        [InlineData(CounterInputImpedance.Ohms1M, "INP:IMP 1E+6", false)]
        public void SetInputImpedance_sends_the_right_command(CounterInputImpedance imp, string expected, bool is50)
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetInputImpedance(imp);
                Assert.Equal(expected, Assert.Single(driver.History));
                Assert.Equal(is50, sim.Is50Ohm);
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void MeasureFrequency_configures_channel_and_returns_the_reading(int channel)
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.Frequency = 10_000_000.0;   // 10 MHz
                var hz = driver.MeasureFrequency(channel);
                Assert.Equal(10_000_000.0, hz, 3);
                Assert.Equal(channel, sim.ConfiguredChannel);
                Assert.Contains($"CONF:FREQ (@{channel})", driver.History);
            }
        }

        [Fact]
        public void MeasureFrequency_arms_via_the_srq_completion_engine()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.Frequency = 1.5e9;
                driver.MeasureFrequency(1);
                // The completion waiter drives the SRQ mask + arm straight onto the session, so they land
                // in the simulator's command log (not the driver History).
                Assert.Contains("*SRE 32", sim.Commands);            // enable mask = Event-Summary bit (32)
                Assert.Contains("*ESE 1;:INIT;*OPC", sim.Commands);  // OPC-arm
                Assert.Contains("*SRE 0", sim.Commands);             // mask cleared afterwards
                Assert.Contains("*ESE 0", sim.Commands);             // restore
                Assert.Contains("FETCH?", driver.History);
            }
        }

        [Fact]
        public void MeasureFrequency_no_signal_times_out_as_typed_exception()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.CompletionTimeoutMs = 120;   // keep the negative path quick
                sim.SignalPresent = false;          // measurement never completes
                var ex = Assert.Throws<Hp53131AException>(() => driver.MeasureFrequency(1));
                Assert.True(ex.IsTimeout);
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(4)]
        public void MeasureFrequency_rejects_out_of_range_channel(int channel)
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.Throws<ArgumentOutOfRangeException>(() => driver.MeasureFrequency(channel));
        }

        [Theory]
        [InlineData("+1.00000000000E+007", 1e7)]
        [InlineData("9.9999950E+06", 9999995.0)]
        [InlineData("0", 0.0)]
        public void ParseFrequency_parses_scientific_notation(string raw, double expected)
        {
            Assert.Equal(expected, Hp53131A.ParseFrequency(raw), 3);
        }

        [Fact]
        public void ParseFrequency_rejects_non_numeric()
        {
            Assert.Throws<FormatException>(() => Hp53131A.ParseFrequency("nope"));
        }

        [Fact]
        public void StatusModel_uses_direct_bit_flow_over_SRE_mask()
        {
            var model = Hp53131A.StatusModel();
            Assert.True(model.SrqSupported);
            Assert.Null(model.RequestServiceBit);                       // direct-bit flow
            Assert.Equal("*SRE {mask}", model.EnableMask.SetCommand);
            Assert.Equal(0x20, model.BitValue("operationComplete"));
            Assert.True(model.Operations.ContainsKey("measureFrequency"));
        }
    }
}
