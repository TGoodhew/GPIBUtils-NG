using System.ComponentModel;
using GpibUtils.Instruments.NoiseFigureMeters;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    public sealed class Hp8970BIdnCommand : Command<Hp8970BIdnCommand.Settings>
    {
        public sealed class Settings : InstrumentSettings { }

        public override int Execute(CommandContext context, Settings settings) => Runner.Guard(() =>
        {
            var session = settings.OpenSession("hp8970b", Hp8970B.DefaultResource);
            using (session) AnsiConsole.MarkupLineInterpolated($"[green]{new Hp8970B(session).Identify()}[/]");
            return 0;
        });
    }

    /// <summary>Tune to a fixed frequency and measure noise figure (and gain).</summary>
    public sealed class Hp8970BMeasureCommand : Command<Hp8970BMeasureCommand.Settings>
    {
        public sealed class Settings : InstrumentSettings
        {
            [CommandOption("-f|--frequency <MHZ>")]
            [Description("Fixed measurement frequency, in MHz.")]
            public double? FrequencyMHz { get; set; }

            [CommandOption("-g|--gain")]
            [Description("Measure corrected NF + Gain (M2) instead of uncorrected NF (M1).")]
            public bool WithGain { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) => Runner.Guard(() =>
        {
            var session = settings.OpenSession("hp8970b", Hp8970B.DefaultResource);
            var d = new Hp8970B(session);
            using (session)
            {
                d.Initialize();
                if (settings.FrequencyMHz.HasValue) d.SetFixedFrequencyMHz(settings.FrequencyMHz.Value);
                d.SetMode(settings.WithGain ? NoiseFigureMode.NoiseFigureAndGain : NoiseFigureMode.NoiseFigure);
                var r = d.Measure();
                foreach (var sent in d.History) AnsiConsole.MarkupLineInterpolated($"[grey]sent[/]: [green]{sent}[/]");
                AnsiConsole.MarkupLineInterpolated($"[green]{r}[/]");
            }
            return 0;
        });
    }
}
