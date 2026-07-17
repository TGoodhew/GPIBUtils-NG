using System;
using System.Collections.Generic;
using System.ComponentModel;
using GpibUtils.Instruments.Analyzers;
using GpibUtils.Instruments.Meters;
using GpibUtils.Instruments.PowerSupplies;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    // ---- generic SCPI spectrum analyzer (dsa800, n9320a) ---------------------

    public abstract class BatchAnalyzerSettings : InstrumentSettings
    {
        [CommandOption("--center <HZ>")] [Description("Center frequency in Hz.")] public double? CenterHz { get; set; }
        [CommandOption("--span <HZ>")] [Description("Frequency span in Hz.")] public double? SpanHz { get; set; }
        [CommandOption("--peak")] [Description("After the sweep, report the peak marker.")] public bool Peak { get; set; }
        internal abstract ISpectrumAnalyzer Open(out Visa.IInstrumentSession session);
        internal abstract IReadOnlyList<string> HistoryOf(ISpectrumAnalyzer a);
    }

    public sealed class BatchAnalyzerSweepCommand<TSettings> : Command<TSettings> where TSettings : BatchAnalyzerSettings
    {
        public override int Execute(CommandContext context, TSettings s) => Runner.Guard(() =>
        {
            var a = s.Open(out var session);
            using (session)
            {
                if (s.CenterHz.HasValue) a.SetCenterFrequencyHz(s.CenterHz.Value);
                if (s.SpanHz.HasValue) a.SetSpanHz(s.SpanHz.Value);
                a.SingleSweep();
                string result = s.Peak ? $"peak {a.MarkerToPeakAmplitude()} at {a.MarkerFrequencyHz() / 1e6:0.###} MHz" : "sweep complete";
                foreach (var sent in s.HistoryOf(a)) AnsiConsole.MarkupLineInterpolated($"[grey]sent[/]: [green]{sent}[/]");
                AnsiConsole.MarkupLineInterpolated($"[green]{result}[/]");
            }
            return 0;
        });
    }

    public sealed class Dsa800AnalyzerSettings : BatchAnalyzerSettings
    {
        internal override ISpectrumAnalyzer Open(out Visa.IInstrumentSession session)
        { session = OpenSession("dsa800", RigolDsa800.DefaultResource); return new RigolDsa800(session); }
        internal override IReadOnlyList<string> HistoryOf(ISpectrumAnalyzer a) => ((RigolDsa800)a).History;
    }

    public sealed class N9320aAnalyzerSettings : BatchAnalyzerSettings
    {
        internal override ISpectrumAnalyzer Open(out Visa.IInstrumentSession session)
        { session = OpenSession("n9320a", AgilentN9320A.DefaultResource); return new AgilentN9320A(session); }
        internal override IReadOnlyList<string> HistoryOf(ISpectrumAnalyzer a) => ((AgilentN9320A)a).History;
    }

    // ---- generic IPowerMeter (hp437b, hp436a) --------------------------------

    public abstract class BatchPowerMeterSettings : InstrumentSettings
    {
        [CommandOption("--zero")] [Description("Zero (and calibrate) the sensor before measuring.")] public bool Zero { get; set; }
        internal abstract IPowerMeter Open(out Visa.IInstrumentSession session);
        internal abstract IReadOnlyList<string> HistoryOf(IPowerMeter p);
    }

    public sealed class BatchPowerMeterMeasureCommand<TSettings> : Command<TSettings> where TSettings : BatchPowerMeterSettings
    {
        public override int Execute(CommandContext context, TSettings s) => Runner.Guard(() =>
        {
            var p = s.Open(out var session);
            using (session)
            {
                p.Initialize();
                if (s.Zero) p.ZeroAndCalibrate();
                double dbm = p.MeasurePowerDbm();
                foreach (var sent in s.HistoryOf(p)) AnsiConsole.MarkupLineInterpolated($"[grey]sent[/]: [green]{sent}[/]");
                AnsiConsole.MarkupLineInterpolated($"[green]{dbm} dBm[/]");
            }
            return 0;
        });
    }

    public sealed class Hp437bPmSettings : BatchPowerMeterSettings
    {
        internal override IPowerMeter Open(out Visa.IInstrumentSession session)
        { session = OpenSession("hp437b", Hp437B.DefaultResource); return new Hp437B(session); }
        internal override IReadOnlyList<string> HistoryOf(IPowerMeter p) => ((Hp437B)p).History;
    }

    public sealed class Hp436aPmSettings : BatchPowerMeterSettings
    {
        internal override IPowerMeter Open(out Visa.IInstrumentSession session)
        { session = OpenSession("hp436a", Hp436A.DefaultResource); return new Hp436A(session); }
        internal override IReadOnlyList<string> HistoryOf(IPowerMeter p) => ((Hp436A)p).History;
    }

    // ---- Keithley 2015 DMM ---------------------------------------------------

    public sealed class Keithley2015MeasureCommand : Command<Keithley2015MeasureCommand.Settings>
    {
        public sealed class Settings : InstrumentSettings
        {
            [CommandArgument(0, "<FUNCTION>")]
            [Description("dcv | acv | dci | aci | ohm2 | ohm4 | freq | period.")]
            public string Function { get; set; }
            [CommandOption("-r|--range <RANGE>")] [Description("Range (optional).")] public string Range { get; set; }
        }

        public override int Execute(CommandContext context, Settings s) => Runner.Guard(() =>
        {
            var session = s.OpenSession("keithley2015", Keithley2015.DefaultResource);
            var d = new Keithley2015(session);
            using (session)
            {
                d.Initialize();
                d.Configure(ParseFunction(s.Function), s.Range);
                double v = d.ReadValue();
                foreach (var sent in d.History) AnsiConsole.MarkupLineInterpolated($"[grey]sent[/]: [green]{sent}[/]");
                AnsiConsole.MarkupLineInterpolated($"[green]{ParseFunction(s.Function)} = {v}[/]");
            }
            return 0;
        });

        private static MeasurementFunction ParseFunction(string f)
        {
            switch ((f ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "dcv": return MeasurementFunction.DcVoltage;
                case "acv": return MeasurementFunction.AcVoltage;
                case "dci": return MeasurementFunction.DcCurrent;
                case "aci": return MeasurementFunction.AcCurrent;
                case "ohm2": return MeasurementFunction.Resistance2Wire;
                case "ohm4": return MeasurementFunction.Resistance4Wire;
                case "freq": return MeasurementFunction.Frequency;
                case "period": return MeasurementFunction.Period;
                default: throw new ArgumentException($"Unknown function '{f}'.");
            }
        }
    }

    // ---- HP 6625A supply -----------------------------------------------------

    public sealed class Hp6625ASetCommand : Command<Hp6625ASetCommand.Settings>
    {
        public sealed class Settings : InstrumentSettings
        {
            [CommandArgument(0, "<VOLTS>")] [Description("Output voltage.")] public double Volts { get; set; }
            [CommandOption("-i|--current <AMPS>")] [Description("Current limit (default 1).")] public double Current { get; set; } = 1;
            [CommandOption("-c|--channel <N>")] [Description("Output 1 or 2 (default 1).")] public int Channel { get; set; } = 1;
            [CommandOption("--off")] [Description("Leave the output off (default: on).")] public bool Off { get; set; }
        }

        public override int Execute(CommandContext context, Settings s) => Runner.Guard(() =>
        {
            var session = s.OpenSession("hp6625a", Hp6625A.DefaultResource);
            var d = new Hp6625A(session) { SelectedChannel = s.Channel };
            using (session)
            {
                d.SetVoltage(s.Volts);
                d.SetCurrentLimit(s.Current);
                d.SetOutput(!s.Off);
                foreach (var sent in d.History) AnsiConsole.MarkupLineInterpolated($"[grey]sent[/]: [green]{sent}[/]");
                AnsiConsole.MarkupLineInterpolated($"[green]CH{s.Channel}: {d.MeasureVoltage()} V / {d.MeasureCurrent()} A[/]");
            }
            return 0;
        });
    }
}
