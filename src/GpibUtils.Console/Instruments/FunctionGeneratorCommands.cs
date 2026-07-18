using System;
using System.ComponentModel;
using GpibUtils.Instruments.SignalSources;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    // ---- shared helpers ------------------------------------------------------

    internal static class FunctionGeneratorSupport
    {
        public static FunctionWaveform ParseWaveform(string s)
        {
            switch ((s ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "sine": case "sin": return FunctionWaveform.Sine;
                case "square": case "squ": return FunctionWaveform.Square;
                case "triangle": case "tri": return FunctionWaveform.Triangle;
                case "ramp": return FunctionWaveform.Ramp;
                case "pulse": case "puls": return FunctionWaveform.Pulse;
                case "noise": return FunctionWaveform.Noise;
                case "dc": return FunctionWaveform.Dc;
                case "arb": case "arbitrary": return FunctionWaveform.Arbitrary;
                default: throw new ArgumentException($"Unknown waveform '{s}'.");
            }
        }

        public static int Run<TDriver>(TDriver driver, Visa.IInstrumentSession session,
            Func<TDriver, string> action, System.Collections.Generic.IReadOnlyList<string> history) => Runner.Guard(() =>
        {
            using (session)
            {
                string result = action(driver);
                foreach (var sent in history)
                    AnsiConsole.MarkupLineInterpolated($"[grey]sent[/]: [green]{sent}[/]");
                if (!string.IsNullOrEmpty(result))
                    AnsiConsole.MarkupLineInterpolated($"[green]{result}[/]");
            }
            return 0;
        });
    }

    /// <summary>Shared "apply" options for a function-generator set command.</summary>
    public abstract class FuncGenApplySettings : InstrumentSettings
    {
        [CommandOption("-w|--waveform <SHAPE>")]
        [Description("sine | square | triangle | ramp | pulse | noise | dc | arb.")]
        public string Waveform { get; set; }

        [CommandOption("-f|--frequency <HZ>")]
        [Description("Frequency in Hz.")]
        public double? FrequencyHz { get; set; }

        [CommandOption("-V|--amplitude <VPP>")]
        [Description("Amplitude in volts peak-to-peak.")]
        public double? AmplitudeVpp { get; set; }

        [CommandOption("-o|--offset <VOLTS>")]
        [Description("DC offset in volts.")]
        public double? OffsetVolts { get; set; }

        [CommandOption("--output <STATE>")]
        [Description("on | off.")]
        public string Output { get; set; }
    }

    internal static class FuncGenApply
    {
        public static void Apply(IFunctionGenerator d, FuncGenApplySettings s)
        {
            if (!string.IsNullOrWhiteSpace(s.Waveform)) d.SetWaveform(FunctionGeneratorSupport.ParseWaveform(s.Waveform));
            if (s.FrequencyHz.HasValue) d.SetFrequencyHz(s.FrequencyHz.Value);
            if (s.AmplitudeVpp.HasValue) d.SetAmplitudeVpp(s.AmplitudeVpp.Value);
            if (s.OffsetVolts.HasValue) d.SetOffsetVolts(s.OffsetVolts.Value);
            if (string.Equals(s.Output, "on", StringComparison.OrdinalIgnoreCase)) d.OutputOn();
            else if (string.Equals(s.Output, "off", StringComparison.OrdinalIgnoreCase)) d.OutputOff();
        }
    }

    // ---- HP 33120A -----------------------------------------------------------

    public sealed class Hp33120AIdnCommand : Command<Hp33120AIdnCommand.Settings>
    {
        public sealed class Settings : InstrumentSettings { }
        public override int Execute(CommandContext context, Settings settings)
        {
            var session = settings.OpenSession("hp33120a", Hp33120A.DefaultResource);
            var d = new Hp33120A(session);
            return FunctionGeneratorSupport.Run(d, session, x => x.Identify(), d.History);
        }
    }

    public sealed class Hp33120AInitCommand : Command<Hp33120AInitCommand.Settings>
    {
        public sealed class Settings : InstrumentSettings { }
        public override int Execute(CommandContext context, Settings settings)
        {
            var session = settings.OpenSession("hp33120a", Hp33120A.DefaultResource);
            var d = new Hp33120A(session);
            return FunctionGeneratorSupport.Run(d, session, x => { x.Initialize(); return null; }, d.History);
        }
    }

    public sealed class Hp33120AApplyCommand : Command<Hp33120AApplyCommand.Settings>
    {
        public sealed class Settings : FuncGenApplySettings { }
        public override int Execute(CommandContext context, Settings settings)
        {
            var session = settings.OpenSession("hp33120a", Hp33120A.DefaultResource);
            var d = new Hp33120A(session);
            return FunctionGeneratorSupport.Run(d, session, x => { FuncGenApply.Apply(x, settings); return "applied"; }, d.History);
        }
    }

    // ---- Rigol DG1000Z -------------------------------------------------------

    public sealed class Dg1000ZIdnCommand : Command<Dg1000ZIdnCommand.Settings>
    {
        public sealed class Settings : InstrumentSettings { }
        public override int Execute(CommandContext context, Settings settings)
        {
            var session = settings.OpenSession("dg1000z", RigolDg1000Z.DefaultResource);
            var d = new RigolDg1000Z(session);
            return FunctionGeneratorSupport.Run(d, session, x => x.Identify(), d.History);
        }
    }

    public sealed class Dg1000ZApplyCommand : Command<Dg1000ZApplyCommand.Settings>
    {
        public sealed class Settings : FuncGenApplySettings
        {
            [CommandOption("-c|--channel <N>")]
            [Description("Output channel 1 or 2 (default 1).")]
            public int Channel { get; set; } = 1;
        }

        public override int Execute(CommandContext context, Settings settings)
        {
            var session = settings.OpenSession("dg1000z", RigolDg1000Z.DefaultResource);
            var d = new RigolDg1000Z(session) { SelectedChannel = settings.Channel };
            return FunctionGeneratorSupport.Run(d, session, x => { FuncGenApply.Apply(x, settings); return "applied"; }, d.History);
        }
    }

    // ---- HP 8116A ------------------------------------------------------------

    public sealed class Hp8116AApplyCommand : Command<Hp8116AApplyCommand.Settings>
    {
        public sealed class Settings : FuncGenApplySettings { }
        public override int Execute(CommandContext context, Settings settings)
        {
            var session = settings.OpenSession("hp8116a", Hp8116A.DefaultResource);
            var d = new Hp8116A(session);
            return FunctionGeneratorSupport.Run(d, session, x => { FuncGenApply.Apply(x, settings); return "applied"; }, d.History);
        }
    }

    public sealed class Hp8116AStatusCommand : Command<Hp8116AStatusCommand.Settings>
    {
        public sealed class Settings : InstrumentSettings { }
        public override int Execute(CommandContext context, Settings settings)
        {
            var session = settings.OpenSession("hp8116a", Hp8116A.DefaultResource);
            var d = new Hp8116A(session);
            return FunctionGeneratorSupport.Run(d, session,
                x => $"status 0x{x.ReadStatusByte():X2}; fault: {x.HasFault()}; error: {x.ReadError()}", d.History);
        }
    }
}
