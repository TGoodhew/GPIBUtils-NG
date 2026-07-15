using System;
using System.ComponentModel;
using System.IO;
using GpibUtils.Instruments.Plotters;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>Shared options for every <c>plotter</c> subcommand (the standard instrument options + model).</summary>
    public class HpPlotterSettings : InstrumentSettings
    {
        [CommandOption("-m|--model <MODEL>")]
        [Description("Plotter model: 7090a (default), 7475a, or 7550a (auto-feed).")]
        public string ModelName { get; set; } = "7090a";

        internal HpPlotterModel Model()
        {
            switch ((ModelName ?? "7090a").Trim().ToLowerInvariant())
            {
                case "7090a": case "7090": return HpPlotterModel.Hp7090A;
                case "7475a": case "7475": return HpPlotterModel.Hp7475A;
                case "7550a": case "7550": return HpPlotterModel.Hp7550A;
                default: throw new ArgumentException($"Unknown plotter model '{ModelName}'. Use 7090a, 7475a, or 7550a.");
            }
        }

        internal HpPlotter OpenDriver(out Visa.IInstrumentSession session)
        {
            session = OpenSession("plotter", HpPlotter.DefaultResource);
            return new HpPlotter(session, Model());
        }
    }

    /// <summary>Shared execution shell: open, run, echo the commands sent, and (optionally) print a result.</summary>
    internal static class HpPlotterRunner
    {
        public static int Run(HpPlotterSettings settings, Func<HpPlotter, string> action) => Runner.Guard(() =>
        {
            var driver = settings.OpenDriver(out var session);
            using (session)
            {
                string result = action(driver);
                // Plots can be long; only echo when a handful of commands were sent.
                if (driver.History.Count <= 12)
                    foreach (var sent in driver.History)
                        AnsiConsole.MarkupLineInterpolated($"[grey]sent[/]: [green]{sent}[/]");
                else
                    AnsiConsole.MarkupLineInterpolated($"[grey]sent {driver.History.Count} HP-GL instructions[/]");
                if (!string.IsNullOrEmpty(result))
                    AnsiConsole.MarkupLineInterpolated($"[green]{result}[/]");
            }
            return 0;
        });
    }

    /// <summary>Show the plotter identity (OI?).</summary>
    public sealed class HpPlotterIdnCommand : Command<HpPlotterIdnCommand.Settings>
    {
        public sealed class Settings : HpPlotterSettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            HpPlotterRunner.Run(settings, d => d.Identify());
    }

    /// <summary>Device clear + HP-GL initialize (IN).</summary>
    public sealed class HpPlotterInitCommand : Command<HpPlotterInitCommand.Settings>
    {
        public sealed class Settings : HpPlotterSettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            HpPlotterRunner.Run(settings, d => { d.Initialize(); return null; });
    }

    /// <summary>Stream an HP-GL plot file to the plotter (and optionally render a PNG preview).</summary>
    public sealed class HpPlotterPlotCommand : Command<HpPlotterPlotCommand.Settings>
    {
        public sealed class Settings : HpPlotterSettings
        {
            [CommandArgument(0, "<file>")]
            [Description("Path to an HP-GL plot file (.plt / .hpgl) to send to the plotter.")]
            public string File { get; set; }

            [CommandOption("--preview <PNG>")]
            [Description("Also render the HP-GL to this PNG (no hardware needed) via the shared renderer.")]
            public string Preview { get; set; }

            [CommandOption("--page")]
            [Description("Advance a fresh page after plotting (auto-feed 7550A only).")]
            public bool Page { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings)
        {
            if (!File.Exists(settings.File))
            {
                AnsiConsole.MarkupLineInterpolated($"[red]Plot file not found:[/] {settings.File}");
                return 1;
            }
            string hpgl = File.ReadAllText(settings.File);

            if (!string.IsNullOrEmpty(settings.Preview))
            {
                File.WriteAllBytes(settings.Preview, HpPlotter.RenderPreview(hpgl));
                AnsiConsole.MarkupLineInterpolated($"[green]preview[/] -> {settings.Preview}");
            }

            return HpPlotterRunner.Run(settings, d =>
            {
                d.PlotHpgl(hpgl);
                if (settings.Page && d.AutoFeed) d.AdvancePage();
                return $"plotted {settings.File} ({d.History.Count} instructions)";
            });
        }
    }

    /// <summary>Read the plotter's hard-clip output window (OW) and scaling points (OP).</summary>
    public sealed class HpPlotterWindowCommand : Command<HpPlotterWindowCommand.Settings>
    {
        public sealed class Settings : HpPlotterSettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            HpPlotterRunner.Run(settings, d =>
            {
                var win = d.OutputWindow();
                var op = d.OutputScalingPoints();
                return $"window (OW): [{string.Join(",", win)}]   scaling points (OP): [{string.Join(",", op)}]";
            });
    }
}
