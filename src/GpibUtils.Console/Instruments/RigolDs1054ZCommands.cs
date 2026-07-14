using System;
using System.ComponentModel;
using System.Globalization;
using GpibUtils.Instruments.Scopes;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>Shared options for every <c>ds1054z</c> subcommand.</summary>
    public class RigolDs1054ZSettings : InstrumentSettings
    {
        internal RigolDs1054Z OpenDriver(out Visa.IInstrumentSession session)
        {
            session = OpenSession("ds1054z", RigolDs1054Z.DefaultResource);
            return new RigolDs1054Z(session);
        }
    }

    internal static class RigolDs1054ZRunner
    {
        public static int Run(RigolDs1054ZSettings settings, Func<RigolDs1054Z, string> action) => Runner.Guard(() =>
        {
            var driver = settings.OpenDriver(out var session);
            using (session)
            {
                string result = action(driver);
                foreach (var sent in driver.History)
                    AnsiConsole.MarkupLineInterpolated($"[grey]sent[/]: [green]{sent}[/]");
                if (!string.IsNullOrEmpty(result))
                    AnsiConsole.MarkupLineInterpolated($"[green]{result}[/]");
            }
            return 0;
        });
    }

    public sealed class RigolDs1054ZIdnCommand : Command<RigolDs1054ZIdnCommand.Settings>
    {
        public sealed class Settings : RigolDs1054ZSettings { }
        public override int Execute(CommandContext context, Settings settings) =>
            RigolDs1054ZRunner.Run(settings, d => d.Identify());
    }

    /// <summary>Run / stop / single / autoscale.</summary>
    public sealed class RigolDs1054ZAcqCommand : Command<RigolDs1054ZAcqCommand.Settings>
    {
        public sealed class Settings : RigolDs1054ZSettings
        {
            [CommandArgument(0, "<action>")]
            [Description("run, stop, single, or auto.")]
            public string Action { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            RigolDs1054ZRunner.Run(settings, d =>
            {
                switch ((settings.Action ?? "").Trim().ToLowerInvariant())
                {
                    case "run": d.Run(); break;
                    case "stop": d.Stop(); break;
                    case "single": d.Single(); break;
                    case "auto": d.AutoScale(); break;
                    default: throw new ArgumentException("Action must be run, stop, single, or auto.");
                }
                return null;
            });
    }

    /// <summary>Turn a channel's display on or off.</summary>
    public sealed class RigolDs1054ZChannelCommand : Command<RigolDs1054ZChannelCommand.Settings>
    {
        public sealed class Settings : RigolDs1054ZSettings
        {
            [CommandArgument(0, "<channel>")]
            [Description("Channel 1-4.")]
            public int Channel { get; set; }

            [CommandArgument(1, "<state>")]
            [Description("on or off.")]
            public string State { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            RigolDs1054ZRunner.Run(settings, d =>
            {
                bool on = (settings.State ?? "").Trim().Equals("on", StringComparison.OrdinalIgnoreCase);
                d.SetChannelDisplay(settings.Channel, on);
                return null;
            });
    }

    /// <summary>Measure peak-to-peak / max / frequency on a channel.</summary>
    public sealed class RigolDs1054ZMeasureCommand : Command<RigolDs1054ZMeasureCommand.Settings>
    {
        public sealed class Settings : RigolDs1054ZSettings
        {
            [CommandArgument(0, "<channel>")]
            [Description("Channel 1-4.")]
            public int Channel { get; set; }

            [CommandOption("--item <ITEM>")]
            [Description("Measurement item: vpp (default), vmax, freq.")]
            public string Item { get; set; } = "vpp";
        }

        public override int Execute(CommandContext context, Settings settings) =>
            RigolDs1054ZRunner.Run(settings, d =>
            {
                double v;
                switch ((settings.Item ?? "vpp").Trim().ToLowerInvariant())
                {
                    case "vpp": v = d.MeasureVpp(settings.Channel); break;
                    case "vmax": v = d.MeasureVmax(settings.Channel); break;
                    case "freq": v = d.MeasureFrequency(settings.Channel); break;
                    default: throw new ArgumentException("Item must be vpp, vmax, or freq.");
                }
                return v.ToString("G6", CultureInfo.InvariantCulture);
            });
    }
}
