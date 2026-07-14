using System;
using System.Collections.Generic;
using System.Linq;
using GpibUtils.Instruments.Counters;
using GpibUtils.Instruments.Meters;
using GpibUtils.Instruments.PowerSupplies;
using GpibUtils.Instruments.SignalSources;
using GpibUtils.Instruments.Switches;

namespace GpibUtils.Console.Instruments
{
    /// <summary>One migrated instrument the CLI can drive: its command-branch key, manual factory-default
    /// resource, and a short description.</summary>
    internal sealed class KnownInstrument
    {
        public string Key { get; }
        public string DefaultResource { get; }
        public string Description { get; }

        public KnownInstrument(string key, string defaultResource, string description)
        {
            Key = key;
            DefaultResource = defaultResource;
            Description = description;
        }
    }

    /// <summary>
    /// The registry of migrated instruments, keyed by CLI branch name (also the key used by the
    /// <see cref="GpibUtils.Common.InstrumentAddressStore"/>). Single source of truth for the
    /// <c>config address</c> commands and the per-device default resource. Add a driver here when its CLI
    /// branch lands so it participates in address configuration.
    /// </summary>
    internal static class KnownInstruments
    {
        public static readonly IReadOnlyList<KnownInstrument> All = new[]
        {
            new KnownInstrument("hp11713a", Hp11713A.DefaultResource, "HP 11713A attenuator/switch driver"),
            new KnownInstrument("hp8340b",  Hp8340B.DefaultResource,  "HP 8340B synthesized sweeper (CW source)"),
            new KnownInstrument("hp8673b",  Hp8673B.DefaultResource,  "HP 8673B synthesized signal generator / LO"),
            new KnownInstrument("hp8902a",  Hp8902A.DefaultResource,  "HP 8902A measuring receiver"),
            new KnownInstrument("hp34401a", Hp34401A.DefaultResource, "HP 34401A digital multimeter"),
            new KnownInstrument("hp53131a", Hp53131A.DefaultResource, "HP 53131A universal frequency counter"),
            new KnownInstrument("hp3499a",  Hp3499A.DefaultResource,  "HP 3499A switch/control system"),
            new KnownInstrument("hpe3633a", HpE3633A.DefaultResource, "HP E3633A DC power supply"),
            new KnownInstrument("dp832",    RigolDp832.DefaultResource, "Rigol DP832 triple DC power supply"),
        };

        public static bool TryGet(string key, out KnownInstrument instrument)
        {
            instrument = string.IsNullOrWhiteSpace(key)
                ? null
                : All.FirstOrDefault(i => string.Equals(i.Key, key.Trim(), StringComparison.OrdinalIgnoreCase));
            return instrument != null;
        }

        /// <summary>Comma-separated list of known device keys, for error messages.</summary>
        public static string KeyList => string.Join(", ", All.Select(i => i.Key));
    }
}
