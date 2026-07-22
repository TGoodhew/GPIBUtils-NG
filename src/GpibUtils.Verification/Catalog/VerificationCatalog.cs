using System;
using System.Collections.Generic;
using GpibUtils.Instruments.Calibrators;
using GpibUtils.Instruments.Counters;
using GpibUtils.Instruments.Meters;
using GpibUtils.Instruments.PowerSupplies;
using GpibUtils.Instruments.SignalSources;
using GpibUtils.Verification.References;
using GpibUtils.Visa;

namespace GpibUtils.Verification.Catalog
{
    /// <summary>A device-under-test the harness can drive: its address-store key, a description, the
    /// factory default resource, and a factory that wraps an open session as the typed DUT.</summary>
    public sealed class InstrumentChoice<TDriver>
    {
        public InstrumentChoice(string key, string description, string defaultResource, Func<IInstrumentSession, TDriver> open)
        {
            Key = key;
            Description = description;
            DefaultResource = defaultResource;
            Open = open;
        }

        /// <summary>Address-store / CLI key (matches <c>KnownInstruments</c>), e.g. "hp8340b".</summary>
        public string Key { get; }
        public string Description { get; }
        public string DefaultResource { get; }

        /// <summary>Wraps an open VISA session as the typed DUT driver.</summary>
        public Func<IInstrumentSession, TDriver> Open { get; }

        public override string ToString() => $"{Key} — {Description}";
    }

    /// <summary>A reference measuring instrument that can fill one measurement role. When a role lists more
    /// than one of these, the harness lets the user pick which reference to verify with.</summary>
    public sealed class ReferenceChoice
    {
        public ReferenceChoice(string key, string description, string defaultResource, ReferenceQuantity quantity,
            Func<IInstrumentSession, IReferenceMeasurement> open)
        {
            Key = key;
            Description = description;
            DefaultResource = defaultResource;
            Quantity = quantity;
            Open = open;
        }

        public string Key { get; }
        public string Description { get; }
        public string DefaultResource { get; }
        public ReferenceQuantity Quantity { get; }

        /// <summary>Wraps an open VISA session as the reference adapter (which then owns the session).</summary>
        public Func<IInstrumentSession, IReferenceMeasurement> Open { get; }

        public override string ToString() => $"{Key} — {Description}";
    }

    /// <summary>
    /// The single source of truth for "what can verify what": which instruments are devices under test in
    /// each category, and which reference instruments can measure each quantity needed to verify them. The
    /// interactive harness and the one-shot CLI both read these lists, so when several instruments can do a
    /// job (e.g. an 8902A, an E4418B, a 438A, a 437B or a 436A can all read a source's RF power) the user
    /// is offered the choice.
    /// </summary>
    public static class VerificationCatalog
    {
        // ---- Signal-source verification ---------------------------------------------------------------

        /// <summary>CW / RF signal generators verifiable via power and/or frequency references.</summary>
        public static IReadOnlyList<InstrumentChoice<ISignalSource>> SignalSourceDuts { get; } =
            new[]
            {
                Src("hp8340b",  "HP 8340B synthesized sweeper",              Hp8340B.DefaultResource,       s => new Hp8340B(s)),
                Src("hp8673b",  "HP 8673B synthesized signal generator",    Hp8673B.DefaultResource,       s => new Hp8673B(s)),
                Src("hp8672a",  "HP 8672A synthesized microwave generator", Hp8672A.DefaultResource,       s => new Hp8672A(s)),
                Src("hp8656",   "HP 8656A/8656B signal generator",          Hp8656.DefaultResource,        s => new Hp8656(s)),
                Src("hp8657b",  "HP 8657B signal generator",                Hp8657B.DefaultResource,       s => new Hp8657B(s)),
                Src("hp8663a",  "HP 8663A synthesized signal generator",    Hp8663A.DefaultResource,       s => new Hp8663A(s)),
                Src("hp8664a",  "HP 8664A signal generator",                Hp8664A.DefaultResource,       s => new Hp8664A(s)),
                Src("hp83620a", "HP 83620A swept-signal generator",         Hp83620A.DefaultResource,      s => new Hp83620A(s)),
                Src("hp83712b", "HP 83712B synthesized CW generator",       Hp83712B.DefaultResource,      s => new Hp83712B(s)),
                Src("e4436b",   "Agilent E4436B ESG-D signal generator",    AgilentE4436B.DefaultResource, s => new AgilentE4436B(s)),
                Src("e4438c",   "Keysight E4438C ESG vector generator",     KeysightE4438C.DefaultResource,s => new KeysightE4438C(s)),
                Src("rs-sme",   "Rohde & Schwarz SME signal generator",     RohdeSchwarzSme.DefaultResource, s => new RohdeSchwarzSme(s)),
                Src("rs-smt",   "Rohde & Schwarz SMT signal generator",     RohdeSchwarzSmt.DefaultResource, s => new RohdeSchwarzSmt(s)),
            };

        /// <summary>Instruments that can measure a source's absolute RF power (dBm).</summary>
        public static IReadOnlyList<ReferenceChoice> RfPowerReferences { get; } =
            new[]
            {
                new ReferenceChoice("hp8902a", "HP 8902A measuring receiver", Hp8902A.DefaultResource, ReferenceQuantity.RfPowerDbm,
                    s => new MeasuringReceiverPowerReference(new Hp8902A(s), s, "HP 8902A measuring receiver")),
                new ReferenceChoice("e4418b", "HP E4418B power meter", HpE4418B.DefaultResource, ReferenceQuantity.RfPowerDbm,
                    s => { var m = new HpE4418B(s); return new PowerMeterReference(m, s, "HP E4418B power meter", f => m.SetFrequencyMHz(f)); }),
                new ReferenceChoice("hp438a", "HP 438A power meter", Hp438A.DefaultResource, ReferenceQuantity.RfPowerDbm,
                    s => new PowerMeterReference(new Hp438A(s), s, "HP 438A power meter")),
                new ReferenceChoice("hp437b", "HP 437B power meter", Hp437B.DefaultResource, ReferenceQuantity.RfPowerDbm,
                    s => new PowerMeterReference(new Hp437B(s), s, "HP 437B power meter")),
                new ReferenceChoice("hp436a", "HP 436A power meter", Hp436A.DefaultResource, ReferenceQuantity.RfPowerDbm,
                    s => new PowerMeterReference(new Hp436A(s), s, "HP 436A power meter")),
            };

        /// <summary>Instruments that can measure a source's frequency (Hz).</summary>
        public static IReadOnlyList<ReferenceChoice> FrequencyReferences { get; } =
            new[]
            {
                new ReferenceChoice("hp53131a", "HP 53131A universal counter", Hp53131A.DefaultResource, ReferenceQuantity.FrequencyHz,
                    s => new FrequencyCounterReference(new Hp53131A(s), s, "HP 53131A universal counter")),
                new ReferenceChoice("hp5351a", "HP 5351A microwave counter", Hp5351A.DefaultResource, ReferenceQuantity.FrequencyHz,
                    s => new LegacyCounterReference(new Hp5351A(s), s, "HP 5351A microwave counter")),
                new ReferenceChoice("hp5342a", "HP 5342A microwave counter", Hp5342A.DefaultResource, ReferenceQuantity.FrequencyHz,
                    s => new LegacyCounterReference(new Hp5342A(s), s, "HP 5342A microwave counter")),
                new ReferenceChoice("hp5343a", "HP 5343A microwave counter", Hp5343A.DefaultResource, ReferenceQuantity.FrequencyHz,
                    s => new LegacyCounterReference(new Hp5343A(s), s, "HP 5343A microwave counter")),
                new ReferenceChoice("hp8902a", "HP 8902A measuring receiver", Hp8902A.DefaultResource, ReferenceQuantity.FrequencyHz,
                    s => new MeasuringReceiverFrequencyReference(new Hp8902A(s), s, "HP 8902A measuring receiver")),
            };

        // ---- DC-source verification -------------------------------------------------------------------

        /// <summary>DC voltage sources (calibrator + power supplies) verifiable via a DMM reference.</summary>
        public static IReadOnlyList<InstrumentChoice<IVoltageSourceDut>> DcSourceDuts { get; } =
            new[]
            {
                new InstrumentChoice<IVoltageSourceDut>("fluke5440", "Fluke 5440A/B DC voltage calibrator", Fluke5440A.DefaultResource,
                    s => new CalibratorVoltageDut(new Fluke5440A(s), "Fluke 5440A calibrator")),
                new InstrumentChoice<IVoltageSourceDut>("hpe3633a", "HP E3633A DC power supply", HpE3633A.DefaultResource,
                    s => new PowerSupplyVoltageDut(new HpE3633A(s), "HP E3633A power supply")),
                new InstrumentChoice<IVoltageSourceDut>("dp832", "Rigol DP832 DC power supply", RigolDp832.DefaultResource,
                    s => new PowerSupplyVoltageDut(new RigolDp832(s), "Rigol DP832 power supply")),
                new InstrumentChoice<IVoltageSourceDut>("hp6625a", "HP 6625A system DC power supply", Hp6625A.DefaultResource,
                    s => new PowerSupplyVoltageDut(new Hp6625A(s), "HP 6625A power supply")),
            };

        /// <summary>Instruments that can measure DC voltage to verify a DC source.</summary>
        public static IReadOnlyList<ReferenceChoice> DcVoltageReferences { get; } =
            new[]
            {
                new ReferenceChoice("hp34401a", "HP 34401A digital multimeter", Hp34401A.DefaultResource, ReferenceQuantity.DcVolts,
                    s => new DmmVoltageReference(new Hp34401A(s), s, "HP 34401A DMM")),
                new ReferenceChoice("dm3058", "Rigol DM3058 digital multimeter", RigolDm3058.DefaultResource, ReferenceQuantity.DcVolts,
                    s => new DmmVoltageReference(new RigolDm3058(s), s, "Rigol DM3058 DMM")),
            };

        private static InstrumentChoice<ISignalSource> Src(string key, string desc, string res, Func<IInstrumentSession, ISignalSource> open) =>
            new InstrumentChoice<ISignalSource>(key, desc, res, open);
    }
}
