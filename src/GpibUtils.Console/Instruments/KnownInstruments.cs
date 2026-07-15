using System;
using System.Collections.Generic;
using System.Linq;
using GpibUtils.Instruments.Analyzers;
using GpibUtils.Instruments.Calibrators;
using GpibUtils.Instruments.Counters;
using GpibUtils.Instruments.Meters;
using GpibUtils.Instruments.PowerSupplies;
using GpibUtils.Instruments.Scopes;
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
            new KnownInstrument("e4418b",   HpE4418B.DefaultResource,  "HP E4418B RF power meter"),
            new KnownInstrument("hp438a",   Hp438A.DefaultResource,    "HP 438A RF power meter"),
            new KnownInstrument("dm3058",   RigolDm3058.DefaultResource, "Rigol DM3058 digital multimeter"),
            new KnownInstrument("hp3458a",  Hp3458A.DefaultResource,   "HP 3458A 8.5-digit DMM"),
            new KnownInstrument("hp5351a",  Hp5351A.DefaultResource,   "HP 5351A microwave frequency counter"),
            new KnownInstrument("hp5342a",  Hp5342A.DefaultResource,   "HP 5342A microwave frequency counter"),
            new KnownInstrument("hp8350b",  Hp8350B.DefaultResource,   "HP 8350B sweep oscillator (CW source)"),
            new KnownInstrument("hp3325b",  Hp3325B.DefaultResource,   "HP 3325B synthesizer/function generator"),
            new KnownInstrument("ds1054z",  RigolDs1054Z.DefaultResource, "Rigol DS1054Z oscilloscope"),
            new KnownInstrument("fluke5440", Fluke5440A.DefaultResource, "Fluke 5440A/5440B DC voltage calibrator"),
            new KnownInstrument("e4438c",   KeysightE4438C.DefaultResource, "Keysight E4438C ESG vector signal generator"),
            new KnownInstrument("hp8560e",  Hp8560E.DefaultResource,   "HP 8560E spectrum analyzer"),
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
