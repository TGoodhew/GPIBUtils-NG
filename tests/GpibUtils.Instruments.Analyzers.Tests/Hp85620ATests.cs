using System;
using System.Linq;
using GpibUtils.Instruments.Analyzers;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.Analyzers.Tests
{
    /// <summary>Drives the <see cref="Hp85620A"/> mass-memory driver against a simulated 8563E+85620A.</summary>
    public class Hp85620ATests
    {
        private static (Hp85620A driver, Hp85620ASimulatedDevice sim, IInstrumentSession session) Bench()
        {
            var provider = new SimulatedGpibProvider();
            var sim = new Hp85620ASimulatedDevice();
            provider.Add(Hp85620A.DefaultResource, sim.Instrument);
            var session = provider.Open(Hp85620A.DefaultResource);
            return (new Hp85620A(session), sim, session);
        }

        [Fact]
        public void Identify_uses_id_query()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.Contains("8563E", driver.Identify());
        }

        [Fact]
        public void Default_address_is_eighteen()
        {
            Assert.Equal("GPIB0::18::INSTR", Hp85620A.DefaultResource);
        }

        [Fact]
        public void Select_device_maps_to_msdev()
        {
            var (driver, _, session) = Bench();
            using (session)
            {
                driver.SelectDevice(MassStorageDevice.Module);
                Assert.Contains("MSDEV MEM;", driver.History);
                driver.SelectDevice(MassStorageDevice.Card);
                Assert.Contains("MSDEV CARD;", driver.History);
            }
        }

        [Fact]
        public void Catalog_module_lists_entries_and_free_bytes()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.AddModuleEntry("STATE1");
                sim.AddModuleEntry("DLP_A");
                sim.BytesFree = 96000;
                var cat = driver.Catalog(MassStorageDevice.Module);
                Assert.Contains("STATE1", cat.Entries);
                Assert.Contains("DLP_A", cat.Entries);
                Assert.Equal(96000, cat.BytesFree);
                Assert.Contains("MSDEV MEM;", driver.History);
                Assert.Contains("CATALOG?;", driver.History);
            }
        }

        [Fact]
        public void Catalog_card_reads_card_device()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.AddCardEntry("CARD_ONLY");
                var cat = driver.Catalog(MassStorageDevice.Card);
                Assert.Contains("CARD_ONLY", cat.Entries);
            }
        }

        [Fact]
        public void Store_to_card_commits_and_checks_error()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.StoreToCard("STATE1");
                Assert.Contains("STATE1", sim.CardEntries);
                Assert.Contains("CARDSTORE %STATE1%;", driver.History);
                Assert.Contains("DONE?;", driver.History);
                Assert.Contains("ERR?;", driver.History);
            }
        }

        [Fact]
        public void Store_to_card_throws_on_analyzer_error()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.PendingError = 116;   // e.g. card full
                var ex = Assert.Throws<Hp85620AException>(() => driver.StoreToCard("STATE1"));
                Assert.Equal(116, ex.ErrorCode);
            }
        }

        [Fact]
        public void Load_from_card_adds_to_module()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.AddCardEntry("STATE2");
                driver.LoadFromCard("STATE2");
                Assert.Contains("STATE2", sim.ModuleEntries);
                Assert.Contains("CARDLOAD %STATE2%;", driver.History);
            }
        }

        [Fact]
        public void Clear_module_disposes_all()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.AddModuleEntry("A");
                sim.AddModuleEntry("B");
                driver.ClearModule();
                Assert.Empty(sim.ModuleEntries);
                Assert.Contains("MSDEV MEM;", driver.History);
                Assert.Contains("DISPOSE ALL;", driver.History);
            }
        }

        [Fact]
        public void Define_function_sends_funcdef_and_checks_error()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.DefineFunction("MYDLP 1 CF 1GHZ;");
                Assert.Single(sim.Functions);
                Assert.Contains("MYDLP", sim.Functions[0]);
            }
        }

        [Fact]
        public void Define_function_throws_on_error()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.PendingError = 2000;   // e.g. syntax error
                Assert.Throws<Hp85620AException>(() => driver.DefineFunction("BAD DLP"));
            }
        }

        [Fact]
        public void Store_rejects_percent_in_name()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.Throws<ArgumentException>(() => driver.StoreToCard("bad%name"));
        }

        [Fact]
        public void Wait_done_reads_done_query()
        {
            var (driver, _, session) = Bench();
            using (session)
            {
                Assert.True(driver.WaitDone());
                Assert.Contains("DONE?;", driver.History);
            }
        }

        [Fact]
        public void Parse_catalog_handles_entries_and_free()
        {
            var cat = Hp85620A.ParseCatalog("STATE1,DLP_A,DLP_B\nBYTES FREE 96000");
            Assert.Equal(3, cat.Entries.Length);
            Assert.Equal(96000, cat.BytesFree);
        }

        [Fact]
        public void Parse_catalog_empty_device()
        {
            var cat = Hp85620A.ParseCatalog("\nBYTES FREE 128000");
            Assert.Empty(cat.Entries);
            Assert.Equal(128000, cat.BytesFree);
        }
    }
}
