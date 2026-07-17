using System;
using System.ComponentModel;
using System.Linq;
using GpibUtils.Instruments.ModulationDomain;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    public sealed class Hp53310AIdnCommand : Command<Hp53310AIdnCommand.Settings>
    {
        public sealed class Settings : InstrumentSettings { }

        public override int Execute(CommandContext context, Settings settings) => Runner.Guard(() =>
        {
            var session = settings.OpenSession("hp53310a", Hp53310A.DefaultResource);
            using (session) AnsiConsole.MarkupLineInterpolated($"[green]{new Hp53310A(session).Identify()}[/]");
            return 0;
        });
    }

    /// <summary>Configure a measurement and read the record array (summary).</summary>
    public sealed class Hp53310AMeasureCommand : Command<Hp53310AMeasureCommand.Settings>
    {
        public sealed class Settings : InstrumentSettings
        {
            [CommandArgument(0, "<TYPE>")]
            [Description("freq | tinterval | freqhist | tinthist.")]
            public string Type { get; set; }

            [CommandOption("-c|--channel <N>")]
            [Description("Input channel 1-3 (default 1; freq types only).")]
            public int Channel { get; set; } = 1;
        }

        public override int Execute(CommandContext context, Settings settings) => Runner.Guard(() =>
        {
            var session = settings.OpenSession("hp53310a", Hp53310A.DefaultResource);
            var d = new Hp53310A(session);
            using (session)
            {
                d.Initialize();
                d.Configure(Parse(settings.Type), settings.Channel);
                var trace = d.Read();
                foreach (var sent in d.History) AnsiConsole.MarkupLineInterpolated($"[grey]sent[/]: [green]{sent}[/]");
                AnsiConsole.MarkupLineInterpolated($"[green]{trace.Count} points, min {trace.Min()} / max {trace.Max()}[/]");
            }
            return 0;
        });

        private static ModulationMeasurement Parse(string t)
        {
            switch ((t ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "freq": return ModulationMeasurement.FrequencyVsTime;
                case "tinterval": case "ti": return ModulationMeasurement.TimeIntervalVsTime;
                case "freqhist": return ModulationMeasurement.FrequencyHistogram;
                case "tinthist": return ModulationMeasurement.TimeIntervalHistogram;
                default: throw new ArgumentException($"Unknown measurement type '{t}'.");
            }
        }
    }
}
