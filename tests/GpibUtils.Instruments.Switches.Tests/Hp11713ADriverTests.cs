using System;
using System.Linq;
using GpibUtils.Instruments.Switches;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.Switches.Tests
{
    public class Hp11713ADriverTests
    {
        // Wires the driver to a simulated 11713A over the standard transport, so every assertion
        // exercises the real write path with no hardware. The simulated device decodes the A/B data
        // strings back into relay state, letting us confirm the exact bytes reached the wire.
        private static (Hp11713A driver, Hp11713ASimulatedDevice sim, IInstrumentSession session) Bench(
            AttenuatorConfig config = null)
        {
            var provider = new SimulatedGpibProvider();
            var sim = new Hp11713ASimulatedDevice();
            provider.Add(Hp11713A.DefaultResource, sim.Instrument);
            var session = provider.Open(Hp11713A.DefaultResource);
            var driver = new Hp11713A(session, config ?? AttenuatorConfig.Default());
            return (driver, sim, session);
        }

        [Fact]
        public void SetAttenuationDb_drives_relays_to_the_requested_value()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                var sent = driver.SetAttenuationDb(30);

                Assert.Equal("A56B123478", sent);                       // 10 + 20 dB engaged
                Assert.Equal(30, driver.State.TotalDecibels(driver.Config));
                Assert.Equal(30, sim.TotalDecibels(driver.Config));     // confirmed on the wire
                Assert.True(driver.State.Engaged.SetEquals(sim.Engaged));
            }
        }

        [Fact]
        public void SetAttenuationDb_zero_bypasses_all_sections()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                var sent = driver.SetAttenuationDb(0);
                Assert.Equal("B12345678", sent);
                Assert.Equal(0, sim.TotalDecibels(driver.Config));
                Assert.Empty(sim.Engaged);
            }
        }

        [Fact]
        public void SetAttenuationDb_out_of_range_throws()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.Throws<ArgumentOutOfRangeException>(() => driver.SetAttenuationDb(200));
        }

        [Fact]
        public void Every_integer_in_range_is_reachable_and_lands_on_the_wire()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                for (int db = 0; db <= driver.Config.MaxDecibels; db++)
                {
                    driver.SetAttenuationDb(db);
                    Assert.Equal(db, sim.TotalDecibels(driver.Config));
                }
            }
        }

        [Fact]
        public void InvertSense_swaps_the_AB_fields_on_the_wire()
        {
            var (driver, _, session) = Bench();
            using (session)
            {
                driver.InvertSense = true;
                var sent = driver.SetEngaged(Array.Empty<int>());
                Assert.Equal("A12345678", sent);   // normally B12345678; sense inverted
            }
        }

        [Fact]
        public void Switches_S9_and_S0_update_state_and_wire()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetSwitch9(true);
                driver.SetSwitch0(false);
                Assert.True(driver.State.Switch9);
                Assert.False(driver.State.Switch0);
                Assert.True(sim.Switch9);
                Assert.False(sim.Switch0);
            }
        }

        [Fact]
        public void Initialize_clears_to_zero_and_records_history()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetAttenuationDb(55);
                driver.Initialize();
                Assert.Equal(0, sim.TotalDecibels(driver.Config));
                Assert.Empty(sim.Engaged);
                Assert.Contains("B12345678", driver.History);
            }
        }

        [Fact]
        public void SendRaw_reaches_the_wire_without_touching_shadow_state()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SendRaw("A1234B5678");
                Assert.Empty(driver.State.Engaged);              // raw is not reflected in tracked state
                Assert.True(sim.Engaged.SetEquals(new[] { 1, 2, 3, 4 }));
            }
        }
    }
}
