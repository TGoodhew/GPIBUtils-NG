using System.Linq;
using GpibUtils.Console.Tui;
using GpibUtils.Visa;
using Xunit;

namespace GpibUtils.Console.Tests
{
    /// <summary>
    /// Unit tests for the interactive TUI's pure core (issue #172): the menu model, the extender-phantom
    /// discovery threshold, provider-row flattening, and the instrument-catalog grouping. These exercise
    /// the TUI logic headlessly (sim-green) — the interactive screens themselves need a terminal.
    /// </summary>
    public class TuiCoreTests
    {
        // ---- Main menu --------------------------------------------------------------------------

        [Fact]
        public void Menu_has_all_screens_with_exit_last()
        {
            var items = TuiMenu.Items;

            Assert.Equal(5, items.Count);
            Assert.Equal(TuiScreen.Exit, items[items.Count - 1].Screen);

            var screens = items.Select(i => i.Screen).ToList();
            Assert.Contains(TuiScreen.Providers, screens);
            Assert.Contains(TuiScreen.Discover, screens);
            Assert.Contains(TuiScreen.Instruments, screens);
            Assert.Contains(TuiScreen.Query, screens);

            Assert.All(items, i => Assert.False(string.IsNullOrWhiteSpace(i.Label)));
            Assert.All(items, i => Assert.False(string.IsNullOrWhiteSpace(i.Description)));
        }

        // ---- Discovery advisory (extender-phantom threshold) ------------------------------------

        [Theory]
        [InlineData(0, false)]
        [InlineData(1, false)]
        [InlineData(14, false)]
        [InlineData(15, true)]
        [InlineData(30, true)]
        public void Extender_phantom_detected_at_or_above_threshold(int count, bool expected)
        {
            Assert.Equal(expected, DiscoveryAdvisory.IsExtenderPhantom(count));
            Assert.Equal(15, DiscoveryAdvisory.ExtenderPhantomThreshold);
        }

        // ---- Provider capability row ------------------------------------------------------------

        [Fact]
        public void Provider_row_flattens_the_simulated_provider()
        {
            var simulated = GpibProviders.Get("Simulated");
            var row = ProviderCapabilityRow.From(simulated, GpibProviders.DefaultProviderName);

            Assert.Equal(simulated.Name, row.Name);
            Assert.True(row.IsAvailable);
            Assert.Equal(simulated.Capabilities.Discovery, row.Discovery);
            Assert.Equal(simulated.Capabilities.ServiceRequest, row.ServiceRequest);
            Assert.Equal(simulated.Capabilities.NativeAddressing, row.NativeAddressing);
        }

        [Fact]
        public void Provider_row_marks_the_registry_default()
        {
            var name = GpibProviders.DefaultProviderName;
            var provider = GpibProviders.Get(name);

            var row = ProviderCapabilityRow.From(provider, name);
            Assert.True(row.IsDefault);

            var notDefault = ProviderCapabilityRow.From(provider, "some-other-name");
            Assert.False(notDefault.IsDefault);
        }

        // ---- Instrument catalog grouping --------------------------------------------------------

        [Fact]
        public void Catalog_groups_partition_every_instrument_exactly_once()
        {
            var all = InstrumentCatalog.All;
            var grouped = InstrumentCatalog.Groups().SelectMany(g => g.Instruments).ToList();

            // No instrument lost, none duplicated.
            Assert.Equal(all.Count, grouped.Count);
            Assert.Equal(
                all.Select(i => i.Key).OrderBy(k => k).ToList(),
                grouped.Select(i => i.Key).OrderBy(k => k).ToList());
        }

        [Fact]
        public void Catalog_groups_are_all_non_empty_and_named()
        {
            foreach (var g in InstrumentCatalog.Groups())
            {
                Assert.False(string.IsNullOrWhiteSpace(g.Name));
                Assert.NotEmpty(g.Instruments);
            }
        }

        [Theory]
        [InlineData("hp34401a", "Digital multimeters")]
        [InlineData("ds1054z", "Oscilloscopes")]
        [InlineData("hpe3633a", "Power supplies")]
        [InlineData("hp53131a", "Frequency counters")]
        [InlineData("hp8560e", "Spectrum analyzers")]
        [InlineData("fluke5440", "Calibrators")]
        [InlineData("maynuo", "Electronic loads")]
        [InlineData("keithley2400", "Source-measure units")]
        public void Catalog_classifies_representative_instruments(string key, string expectedFamily)
        {
            var instrument = InstrumentCatalog.All.Single(i => i.Key == key);
            Assert.Equal(expectedFamily, InstrumentCatalog.FamilyOf(instrument));
        }

        [Fact]
        public void Catalog_has_no_other_bucket()
        {
            // Every known instrument should land in a real family, not the "Other" fallback.
            Assert.DoesNotContain(InstrumentCatalog.Groups(), g => g.Name == "Other");
        }
    }
}
