using System;
using System.IO;
using GpibUtils.Common;
using Xunit;

namespace GpibUtils.Common.Tests
{
    public class InstrumentAddressStoreTests : IDisposable
    {
        private readonly string _path;

        public InstrumentAddressStoreTests()
        {
            _path = Path.Combine(Path.GetTempPath(), "gpibutils-addr-" + Guid.NewGuid().ToString("N") + ".json");
        }

        public void Dispose()
        {
            try { if (File.Exists(_path)) File.Delete(_path); } catch { /* best effort */ }
        }

        [Fact]
        public void Load_missing_file_returns_empty_store()
        {
            var store = InstrumentAddressStore.Load(_path);
            Assert.Empty(store.Entries);
            Assert.False(store.TryGet("hp8340b", out _));
        }

        [Fact]
        public void Set_then_TryGet_returns_value()
        {
            var store = InstrumentAddressStore.Load(_path);
            store.Set("hp8340b", "GPIB0::20::INSTR");
            Assert.True(store.TryGet("hp8340b", out var r));
            Assert.Equal("GPIB0::20::INSTR", r);
        }

        [Fact]
        public void Set_and_TryGet_are_case_insensitive_and_trim()
        {
            var store = InstrumentAddressStore.Load(_path);
            store.Set("  HP8902A ", "  GPIB0::14::INSTR  ");
            Assert.True(store.TryGet("hp8902a", out var r));
            Assert.Equal("GPIB0::14::INSTR", r);
        }

        [Fact]
        public void Save_then_Load_round_trips_entries()
        {
            var store = InstrumentAddressStore.Load(_path);
            store.Set("hp8340b", "GPIB0::20::INSTR");
            store.Set("hp8902a", "GPIB0::14::INSTR");
            store.Save();

            Assert.True(File.Exists(_path));
            var reloaded = InstrumentAddressStore.Load(_path);
            Assert.Equal(2, reloaded.Entries.Count);
            Assert.True(reloaded.TryGet("hp8340b", out var a));
            Assert.Equal("GPIB0::20::INSTR", a);
            Assert.True(reloaded.TryGet("hp8902a", out var b));
            Assert.Equal("GPIB0::14::INSTR", b);
        }

        [Fact]
        public void Saved_file_is_valid_readable_json()
        {
            var store = InstrumentAddressStore.Load(_path);
            store.Set("hp8673b", "GPIB0::19::INSTR");
            store.Save();

            var text = File.ReadAllText(_path);
            Assert.Contains("addresses", text);
            Assert.Contains("hp8673b", text);
            Assert.Contains("GPIB0::19::INSTR", text);
        }

        [Fact]
        public void Remove_deletes_the_override()
        {
            var store = InstrumentAddressStore.Load(_path);
            store.Set("hp8340b", "GPIB0::20::INSTR");
            Assert.True(store.Remove("hp8340b"));
            Assert.False(store.TryGet("hp8340b", out _));
            Assert.False(store.Remove("hp8340b"));   // already gone
        }

        [Theory]
        // explicit wins over everything
        [InlineData("GPIB0::5::INSTR", "GPIB0::20::INSTR", "GPIB0::5::INSTR")]
        // no explicit -> configured wins over default
        [InlineData(null, "GPIB0::20::INSTR", "GPIB0::20::INSTR")]
        [InlineData("  ", "GPIB0::20::INSTR", "GPIB0::20::INSTR")]
        public void Resolve_prefers_explicit_then_config(string explicitAddr, string configured, string expected)
        {
            var store = InstrumentAddressStore.Load(_path);
            store.Set("hp8340b", configured);
            Assert.Equal(expected, store.Resolve(explicitAddr, "hp8340b", "GPIB0::99::INSTR"));
        }

        [Fact]
        public void Resolve_falls_back_to_default_when_nothing_configured()
        {
            var store = InstrumentAddressStore.Load(_path);
            Assert.Equal("GPIB0::14::INSTR", store.Resolve(null, "hp8902a", "GPIB0::14::INSTR"));
        }

        [Fact]
        public void Set_rejects_blank_device_or_resource()
        {
            var store = InstrumentAddressStore.Load(_path);
            Assert.Throws<ArgumentException>(() => store.Set("", "GPIB0::1::INSTR"));
            Assert.Throws<ArgumentException>(() => store.Set("hp8340b", " "));
        }
    }
}
