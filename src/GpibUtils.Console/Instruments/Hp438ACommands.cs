using System;
using System.ComponentModel;
using System.Globalization;
using GpibUtils.Instruments.Meters;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>Shared options for every <c>hp438a</c> subcommand.</summary>
    public class Hp438ASettings : InstrumentSettings
    {
        internal Hp438A OpenDriver(out Visa.IInstrumentSession session)
        {
            session = OpenSession("hp438a", Hp438A.DefaultResource);
            return new Hp438A(session);
        }
    }

    internal static class Hp438ARunner
    {
        public static int Run(Hp438ASettings settings, Func<Hp438A, string> action) => Runner.Guard(() =>
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

    public sealed class Hp438AIdnCommand : Command<Hp438AIdnCommand.Settings>
    {
        public sealed class Settings : Hp438ASettings { }
        public override int Execute(CommandContext context, Settings settings) =>
            Hp438ARunner.Run(settings, d => d.Identify());
    }

    public sealed class Hp438AInitCommand : Command<Hp438AInitCommand.Settings>
    {
        public sealed class Settings : Hp438ASettings { }
        public override int Execute(CommandContext context, Settings settings) =>
            Hp438ARunner.Run(settings, d => { d.Initialize(); return null; });
    }

    public sealed class Hp438AZeroCommand : Command<Hp438AZeroCommand.Settings>
    {
        public sealed class Settings : Hp438ASettings { }
        public override int Execute(CommandContext context, Settings settings) =>
            Hp438ARunner.Run(settings, d => { d.ZeroAndCalibrate(); return "zeroed"; });
    }

    /// <summary>Measure power (dBm) on channel A or B.</summary>
    public sealed class Hp438AMeasureCommand : Command<Hp438AMeasureCommand.Settings>
    {
        public sealed class Settings : Hp438ASettings
        {
            [CommandArgument(0, "[channel]")]
            [Description("Sensor channel A or B (default A).")]
            public string Channel { get; set; } = "A";
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp438ARunner.Run(settings, d =>
            {
                char ch = string.IsNullOrEmpty(settings.Channel) ? 'A' : settings.Channel.Trim()[0];
                return $"{d.MeasurePowerDbm(ch).ToString("0.###", CultureInfo.InvariantCulture)} dBm";
            });
    }
}
