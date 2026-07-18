using System;
using System.Collections.Generic;
using System.Linq;
using GpibUtils.Console.Instruments;

namespace GpibUtils.Console.Tui
{
    /// <summary>One instrument in the browser: its CLI/address key, description, and manual-default resource.</summary>
    public sealed class CatalogInstrument
    {
        public string Key { get; }
        public string Description { get; }
        public string DefaultResource { get; }

        public CatalogInstrument(string key, string description, string defaultResource)
        {
            Key = key;
            Description = description;
            DefaultResource = defaultResource;
        }

        public override string ToString() => Key;
    }

    /// <summary>A named family of instruments for the two-level instrument browser.</summary>
    public sealed class CatalogGroup
    {
        public string Name { get; }
        public IReadOnlyList<CatalogInstrument> Instruments { get; }

        public CatalogGroup(string name, IReadOnlyList<CatalogInstrument> instruments)
        {
            Name = name;
            Instruments = instruments;
        }
    }

    /// <summary>
    /// A presentation-only view of <see cref="KnownInstruments"/> for the TUI instrument browser: the same
    /// device keys + manual-default resources, grouped into families by keyword so the user can drill down
    /// family → instrument. Grouping is pure and total (every known instrument lands in exactly one group),
    /// so it is unit-testable. Selecting an instrument only pre-fills its resource into the query console —
    /// no capability is added here that the CLI (<c>config address</c> / <c>query</c>) doesn't already have.
    /// </summary>
    public static class InstrumentCatalog
    {
        // Ordered family rules: (display name, keyword matchers). Each instrument is assigned to the FIRST
        // family whose keyword its description matches, so the mapping is deterministic and non-overlapping.
        private static readonly (string Name, string[] Keywords)[] Families =
        {
            ("Oscilloscopes",                 new[] { "oscilloscope" }),
            ("Spectrum analyzers",            new[] { "spectrum analyzer" }),
            ("Network analyzers",             new[] { "network analyzer" }),
            ("Frequency counters",            new[] { "counter" }),
            ("Power supplies",                new[] { "power supply" }),
            ("Power meters / receivers",      new[] { "power meter", "measuring receiver" }),
            ("Digital multimeters",           new[] { "multimeter", "dmm" }),
            ("LCR meters",                    new[] { "lcr" }),
            ("Electronic loads",              new[] { "electronic load" }),
            ("Calibrators",                   new[] { "calibrator" }),
            ("Source-measure units",          new[] { "sourcemeter", "smu" }),
            ("Switches / attenuators",        new[] { "attenuator", "switch" }),
            ("Plotters / memory",             new[] { "plotter", "mass memory" }),
            ("Modulation / audio / RF analyzers", new[] { "modulation", "audio analyzer", "noise figure", "vector voltmeter", "vsa", "transmitter tester" }),
            ("Signal sources",                new[] { "signal generator", "generator", "source", "sweeper", "sweep oscillator", "synthesizer", "level generator", "esg" }),
        };

        private const string OtherFamily = "Other";

        /// <summary>Every known instrument, in registry order.</summary>
        public static IReadOnlyList<CatalogInstrument> All { get; } = KnownInstruments.All
            .Select(i => new CatalogInstrument(i.Key, i.Description, i.DefaultResource))
            .ToList();

        /// <summary>The family this instrument's description falls into (first-match; <c>Other</c> if none).</summary>
        public static string FamilyOf(CatalogInstrument instrument)
        {
            if (instrument == null) throw new ArgumentNullException(nameof(instrument));
            var text = (instrument.Description ?? string.Empty).ToLowerInvariant();
            foreach (var f in Families)
                if (f.Keywords.Any(k => text.Contains(k)))
                    return f.Name;
            return OtherFamily;
        }

        /// <summary>The catalog grouped into non-empty families, in family order (Other last if present).</summary>
        public static IReadOnlyList<CatalogGroup> Groups()
        {
            var byFamily = All.GroupBy(FamilyOf).ToDictionary(g => g.Key, g => (IReadOnlyList<CatalogInstrument>)g.ToList());

            var ordered = new List<CatalogGroup>();
            foreach (var f in Families)
                if (byFamily.TryGetValue(f.Name, out var items))
                    ordered.Add(new CatalogGroup(f.Name, items));
            if (byFamily.TryGetValue(OtherFamily, out var other))
                ordered.Add(new CatalogGroup(OtherFamily, other));
            return ordered;
        }
    }
}
