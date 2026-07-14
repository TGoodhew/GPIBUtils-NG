using System;
using System.ComponentModel;
using System.Globalization;
using GpibUtils.Instruments.PowerSupplies;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>Shared options for every <c>dp832</c> subcommand (the standard instrument options).</summary>
    public class RigolDp832Settings : InstrumentSettings
    {
        internal RigolDp832 OpenDriver(out Visa.IInstrumentSession session)
        {
            session = OpenSession("dp832", RigolDp832.DefaultResource);
            return new RigolDp832(session);
        }
    }

    /// <summary>Options for commands that target a channel.</summary>
    public class RigolDp832ChannelSettings : RigolDp832Settings
    {
        [CommandOption("-c|--channel <N>")]
        [Description("Output channel 1-3 (default 1).")]
        public int Channel { get; set; } = 1;
    }

    internal static class RigolDp832Runner
    {
        public static int Run(RigolDp832Settings settings, Func<RigolDp832, string> action) => Runner.Guard(() =>
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

    public sealed class RigolDp832IdnCommand : Command<RigolDp832IdnCommand.Settings>
    {
        public sealed class Settings : RigolDp832Settings { }
        public override int Execute(CommandContext context, Settings settings) =>
            RigolDp832Runner.Run(settings, d => d.Identify());
    }

    public sealed class RigolDp832InitCommand : Command<RigolDp832InitCommand.Settings>
    {
        public sealed class Settings : RigolDp832Settings { }
        public override int Execute(CommandContext context, Settings settings) =>
            RigolDp832Runner.Run(settings, d => { d.Initialize(); return null; });
    }

    /// <summary>Set a channel's voltage and current limit, optionally enabling the output.</summary>
    public sealed class RigolDp832SetCommand : Command<RigolDp832SetCommand.Settings>
    {
        public sealed class Settings : RigolDp832ChannelSettings
        {
            [CommandArgument(0, "<volts>")]
            [Description("Output voltage in volts.")]
            public double Volts { get; set; }

            [CommandOption("-i|--current <AMPS>")]
            [Description("Current limit in amps.")]
            public double? Amps { get; set; }

            [CommandOption("--on")]
            [Description("Enable the output after setting.")]
            public bool On { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            RigolDp832Runner.Run(settings, d =>
            {
                d.SetVoltage(settings.Channel, settings.Volts);
                if (settings.Amps.HasValue) d.SetCurrentLimit(settings.Channel, settings.Amps.Value);
                if (settings.On) d.SetOutput(settings.Channel, true);
                return null;
            });
    }

    /// <summary>Enable or disable a channel's output.</summary>
    public sealed class RigolDp832OutputCommand : Command<RigolDp832OutputCommand.Settings>
    {
        public sealed class Settings : RigolDp832ChannelSettings
        {
            [CommandArgument(0, "<state>")]
            [Description("on or off.")]
            public string State { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            RigolDp832Runner.Run(settings, d =>
            {
                bool on = settings.State != null && settings.State.Trim().Equals("on", StringComparison.OrdinalIgnoreCase);
                d.SetOutput(settings.Channel, on);
                return null;
            });
    }

    /// <summary>Measure a channel's voltage, current, and power.</summary>
    public sealed class RigolDp832MeasureCommand : Command<RigolDp832MeasureCommand.Settings>
    {
        public sealed class Settings : RigolDp832ChannelSettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            RigolDp832Runner.Run(settings, d =>
            {
                var c = settings.Channel;
                return string.Format(CultureInfo.InvariantCulture, "CH{0}: {1:G6} V, {2:G6} A, {3:G6} W",
                    c, d.MeasureVoltage(c), d.MeasureCurrent(c), d.MeasurePower(c));
            });
    }
}
