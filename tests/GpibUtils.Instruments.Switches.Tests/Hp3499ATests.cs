using System;
using System.Linq;
using GpibUtils.Instruments.Switches;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.Switches.Tests
{
    /// <summary>
    /// Drives the <see cref="Hp3499A"/> driver against a simulated 3499A over the standard transport, so
    /// assertions exercise the real write / query path (relay open/close, state query, card inventory) with
    /// no hardware.
    /// </summary>
    public class Hp3499ATests
    {
        private static (Hp3499A driver, Hp3499ASimulatedDevice sim, IInstrumentSession session) Bench(
            Action<Hp3499ASimulatedDevice> configure = null)
        {
            var provider = new SimulatedGpibProvider();
            var sim = new Hp3499ASimulatedDevice();
            configure?.Invoke(sim);
            provider.Add(Hp3499A.DefaultResource, sim.Instrument);
            var session = provider.Open(Hp3499A.DefaultResource);
            return (new Hp3499A(session), sim, session);
        }

        [Fact]
        public void Default_resource_is_factory_gpib_9()
        {
            Assert.Equal("GPIB0::9::INSTR", Hp3499A.DefaultResource);
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
                Assert.Contains("3499A", driver.Identify());
        }

        [Fact]
        public void ChannelAddress_composes_slot_and_channel()
        {
            Assert.Equal(100, Hp3499A.ChannelAddress(1, 0));
            Assert.Equal(213, Hp3499A.ChannelAddress(2, 13));
        }

        [Theory]
        [InlineData(100)]
        [InlineData(213)]
        public void ChannelAddress_rejects_channel_over_99(int _)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Hp3499A.ChannelAddress(1, 100));
        }

        [Fact]
        public void Close_and_open_track_relay_state()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.Close(100);
                Assert.Equal("ROUT:CLOS (@100)", Assert.Single(driver.History));
                Assert.True(sim.IsClosed(100));

                driver.Open(100);
                Assert.Contains("ROUT:OPEN (@100)", driver.History);
                Assert.False(sim.IsClosed(100));
            }
        }

        [Fact]
        public void IsClosed_reflects_the_relay_state()
        {
            var (driver, _, session) = Bench();
            using (session)
            {
                Assert.False(driver.IsClosed(113));
                driver.Close(113);
                Assert.True(driver.IsClosed(113));
                Assert.Contains("ROUT:CLOS? (@113)", driver.History);
            }
        }

        [Fact]
        public void Reset_opens_all_relays()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.Close(100);
                driver.Close(101);
                Assert.Equal(2, sim.ClosedChannels.Count);
                driver.Reset();
                Assert.Empty(sim.ClosedChannels);
            }
        }

        [Fact]
        public void GetCardType_reads_the_configured_slot()
        {
            var (driver, _, session) = Bench(s => s
                .WithCard(1, "HEWLETT-PACKARD,44472A,0,1.0")
                .WithCard(2, "HEWLETT-PACKARD,44476B,0,1.0"));
            using (session)
            {
                Assert.Contains("44472A", driver.GetCardType(1));
                Assert.Contains("44476B", driver.GetCardType(2));
                Assert.Contains("SYST:CTYPE? 1", driver.History);
            }
        }

        [Fact]
        public void ListCards_enumerates_slots_with_empty_default()
        {
            var (driver, _, session) = Bench(s => s.WithCard(1, "HEWLETT-PACKARD,44472A,0,1.0"));
            using (session)
            {
                var cards = driver.ListCards(3);
                Assert.Equal(3, cards.Count);
                Assert.Equal(new[] { 0, 1, 2 }, cards.Select(c => c.Slot).ToArray());
                Assert.Equal("0,0,0,0", cards[0].CardType);          // empty slot default
                Assert.Contains("44472A", cards[1].CardType);
            }
        }

        [Theory]
        [InlineData("1", true)]
        [InlineData("0", false)]
        [InlineData("+1", true)]
        [InlineData("ON", true)]
        [InlineData("OFF", false)]
        public void ParseBooleanReply_reads_closed_state(string reply, bool expected)
        {
            Assert.Equal(expected, Hp3499A.ParseBooleanReply(reply));
        }

        [Fact]
        public void ParseBooleanReply_rejects_garbage()
        {
            Assert.Throws<FormatException>(() => Hp3499A.ParseBooleanReply("maybe"));
        }
    }
}
