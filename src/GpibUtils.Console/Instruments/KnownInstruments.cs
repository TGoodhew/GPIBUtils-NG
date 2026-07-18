using System;
using System.Collections.Generic;
using System.Linq;
using GpibUtils.Instruments.Analyzers;
using GpibUtils.Instruments.Audio;
using GpibUtils.Instruments.Calibrators;
using GpibUtils.Instruments.Counters;
using GpibUtils.Instruments.ElectronicLoads;
using GpibUtils.Instruments.LcrMeters;
using GpibUtils.Instruments.Meters;
using GpibUtils.Instruments.ModulationDomain;
using GpibUtils.Instruments.NoiseFigureMeters;
using GpibUtils.Instruments.NetworkAnalyzers;
using GpibUtils.Instruments.Plotters;
using GpibUtils.Instruments.PowerSupplies;
using GpibUtils.Instruments.Scopes;
using GpibUtils.Instruments.SourceMeasure;
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
            new KnownInstrument("hp8672a",  Hp8672A.DefaultResource,  "HP 8672A synthesized microwave signal generator"),
            new KnownInstrument("hp33120a", Hp33120A.DefaultResource, "HP 33120A function/arbitrary waveform generator"),
            new KnownInstrument("dg1000z",  RigolDg1000Z.DefaultResource, "Rigol DG1000Z function/arbitrary waveform generator"),
            new KnownInstrument("hp8116a",  Hp8116A.DefaultResource,  "HP 8116A pulse/function generator"),
            new KnownInstrument("e4436b",   AgilentE4436B.DefaultResource, "Agilent E4436B ESG-D signal generator"),
            new KnownInstrument("hp83620a", Hp83620A.DefaultResource, "HP 83620A synthesized swept-signal generator"),
            new KnownInstrument("hp83712b", Hp83712B.DefaultResource, "HP 83712B synthesized CW generator"),
            new KnownInstrument("hp8656",   Hp8656.DefaultResource,   "HP 8656A/8656B signal generator"),
            new KnownInstrument("hp8663a",  Hp8663A.DefaultResource,  "HP 8663A synthesized signal generator"),
            new KnownInstrument("hp3335a",  Hp3335A.DefaultResource,  "HP 3335A synthesizer/level generator (listen-only)"),
            new KnownInstrument("hp3245a",  Hp3245A.DefaultResource,  "HP 3245A universal source (DC V/I + waveform)"),
            new KnownInstrument("hp8657b",  Hp8657B.DefaultResource,  "HP 8657B signal generator"),
            new KnownInstrument("hp8664a",  Hp8664A.DefaultResource,  "HP 8664A signal generator (HP-SL)"),
            new KnownInstrument("rs-sme",   RohdeSchwarzSme.DefaultResource, "Rohde & Schwarz SME signal generator"),
            new KnownInstrument("rs-smt",   RohdeSchwarzSmt.DefaultResource, "Rohde & Schwarz SMT signal generator"),
            new KnownInstrument("hp8902a",  Hp8902A.DefaultResource,  "HP 8902A measuring receiver"),
            new KnownInstrument("hp34401a", Hp34401A.DefaultResource, "HP 34401A digital multimeter"),
            new KnownInstrument("hp53131a", Hp53131A.DefaultResource, "HP 53131A universal frequency counter"),
            new KnownInstrument("hp3499a",  Hp3499A.DefaultResource,  "HP 3499A switch/control system"),
            new KnownInstrument("hpe3633a", HpE3633A.DefaultResource, "HP E3633A DC power supply"),
            new KnownInstrument("dp832",    RigolDp832.DefaultResource, "Rigol DP832 triple DC power supply"),
            new KnownInstrument("e4418b",   HpE4418B.DefaultResource,  "HP E4418B RF power meter"),
            new KnownInstrument("hp438a",   Hp438A.DefaultResource,    "HP 438A RF power meter"),
            new KnownInstrument("hp5005b",  Hp5005B.DefaultResource,   "HP 5005B signature multimeter"),
            new KnownInstrument("hp4275a",  Hp4275A.DefaultResource,   "HP 4275A multi-frequency LCR meter"),
            new KnownInstrument("hp8903b",  Hp8903B.DefaultResource,   "HP 8903B audio analyzer"),
            new KnownInstrument("dm3058",   RigolDm3058.DefaultResource, "Rigol DM3058 digital multimeter"),
            new KnownInstrument("hp3458a",  Hp3458A.DefaultResource,   "HP 3458A 8.5-digit DMM"),
            new KnownInstrument("hp5351a",  Hp5351A.DefaultResource,   "HP 5351A microwave frequency counter"),
            new KnownInstrument("hp5342a",  Hp5342A.DefaultResource,   "HP 5342A microwave frequency counter"),
            new KnownInstrument("hp5343a",  Hp5343A.DefaultResource,   "HP 5343A microwave frequency counter (26.5 GHz)"),
            new KnownInstrument("hp8350b",  Hp8350B.DefaultResource,   "HP 8350B sweep oscillator (CW source)"),
            new KnownInstrument("hp3325b",  Hp3325B.DefaultResource,   "HP 3325B synthesizer/function generator"),
            new KnownInstrument("ds1054z",  RigolDs1054Z.DefaultResource, "Rigol DS1054Z oscilloscope"),
            new KnownInstrument("dpo3000",  TektronixDpo3000.DefaultResource, "Tektronix DPO3000/MSO3000 oscilloscope"),
            new KnownInstrument("dpo4000",  TektronixDpo4000.DefaultResource, "Tektronix DPO4000/MSO4000 oscilloscope"),
            new KnownInstrument("tds784",   TektronixTds784.DefaultResource, "Tektronix TDS784C/D oscilloscope"),
            new KnownInstrument("hp54622",  Hp54622A.DefaultResource,  "HP/Agilent 54622A/D oscilloscope"),
            new KnownInstrument("hp54845a", Hp54845A.DefaultResource,  "Agilent 54845A Infiniium oscilloscope"),
            new KnownInstrument("lc574a",   LeCroyLC574A.DefaultResource, "LeCroy LC574A oscilloscope"),
            new KnownInstrument("waverunner6000", LeCroyWaveRunner6000.DefaultResource, "LeCroy WaveRunner 6000 oscilloscope"),
            new KnownInstrument("fluke5440", Fluke5440A.DefaultResource, "Fluke 5440A/5440B DC voltage calibrator"),
            new KnownInstrument("e4438c",   KeysightE4438C.DefaultResource, "Keysight E4438C ESG vector signal generator"),
            new KnownInstrument("hp8560e",  Hp8560E.DefaultResource,   "HP 8560E spectrum analyzer"),
            new KnownInstrument("hp8591e",  Hp8591E.DefaultResource,   "HP 8591E spectrum analyzer (8590 family)"),
            new KnownInstrument("dsa800",   RigolDsa800.DefaultResource, "Rigol DSA800 spectrum analyzer"),
            new KnownInstrument("n9320a",   AgilentN9320A.DefaultResource, "Agilent N9320A spectrum analyzer"),
            new KnownInstrument("hp437b",   Hp437B.DefaultResource,    "HP 437B RF power meter"),
            new KnownInstrument("hp436a",   Hp436A.DefaultResource,    "HP 436A power meter"),
            new KnownInstrument("hp8508a",  Hp8508A.DefaultResource,   "HP 8508A vector voltmeter"),
            new KnownInstrument("maynuo",   MaynuoM9811.DefaultResource, "Maynuo M9811 DC electronic load (Modbus/serial)"),
            new KnownInstrument("hp8901",   Hp8901.DefaultResource,    "HP 8901A/8901B modulation analyzer"),
            new KnownInstrument("hp8970b",  Hp8970B.DefaultResource,   "HP 8970B noise figure meter"),
            new KnownInstrument("keithley2015", Keithley2015.DefaultResource, "Keithley 2015 THD multimeter"),
            new KnownInstrument("hp6625a",  Hp6625A.DefaultResource,   "HP 6625A system DC power supply"),
            new KnownInstrument("hp8714",   Hp8714.DefaultResource,    "HP 8711C-8714C RF network analyzer"),
            new KnownInstrument("hp8720c",  Hp8720C.DefaultResource,   "HP 8720C microwave vector network analyzer"),
            new KnownInstrument("hp8757d",  Hp8757D.DefaultResource,   "HP 8757D scalar network analyzer"),
            new KnownInstrument("keithley2400", Keithley2400.DefaultResource, "Keithley 2400 SourceMeter SMU"),
            new KnownInstrument("hp53310a", Hp53310A.DefaultResource,  "HP 53310A modulation domain analyzer"),
            new KnownInstrument("hp3585",   Hp3585.DefaultResource,    "HP 3585A/3585B spectrum analyzer"),
            new KnownInstrument("e4406a",   AgilentE4406A.DefaultResource, "Agilent E4406A VSA transmitter tester"),
            new KnownInstrument("hp85620a", Hp85620A.DefaultResource,  "HP 85620A mass memory module (via 8563E)"),
            new KnownInstrument("plotter",  HpPlotter.DefaultResource, "HP 7090A/7475A/7550A HP-GL pen plotter"),
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
