using System;
using System.ComponentModel;
using System.Globalization;
using GpibUtils.Instruments.Meters;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>Shared options for every <c>e4418b</c> subcommand.</summary>
    public class HpE4418BSettings : InstrumentSettings
    {
        internal HpE4418B OpenDriver(out Visa.IInstrumentSession session)
        {
            session = OpenSession("e4418b", HpE4418B.DefaultResource);
            return new HpE4418B(session);
        }
    }

    internal static class HpE4418BRunner
    {
        public static int Run(HpE4418BSettings settings, Func<HpE4418B, string> action) => Runner.Guard(() =>
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

    public sealed class HpE4418BIdnCommand : Command<HpE4418BIdnCommand.Settings>
    {
        public sealed class Settings : HpE4418BSettings { }
        public override int Execute(CommandContext context, Settings settings) =>
            HpE4418BRunner.Run(settings, d => d.Identify());
    }

    public sealed class HpE4418BInitCommand : Command<HpE4418BInitCommand.Settings>
    {
        public sealed class Settings : HpE4418BSettings { }
        public override int Execute(CommandContext context, Settings settings) =>
            HpE4418BRunner.Run(settings, d => { d.Initialize(); return null; });
    }

    /// <summary>Zero and calibrate the sensor.</summary>
    public sealed class HpE4418BCalCommand : Command<HpE4418BCalCommand.Settings>
    {
        public sealed class Settings : HpE4418BSettings { }
        public override int Execute(CommandContext context, Settings settings) =>
            HpE4418BRunner.Run(settings, d => { d.ZeroAndCalibrate(); return "zeroed + calibrated"; });
    }

    /// <summary>Measure power (dBm) at a carrier frequency.</summary>
    public sealed class HpE4418BMeasureCommand : Command<HpE4418BMeasureCommand.Settings>
    {
        public sealed class Settings : HpE4418BSettings
        {
            [CommandArgument(0, "<mhz>")]
            [Description("Carrier frequency in MHz (applies the sensor cal factor).")]
            public double Mhz { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            HpE4418BRunner.Run(settings, d =>
            {
                d.SetFrequencyMHz(settings.Mhz);
                return $"{d.MeasurePowerDbm().ToString("0.###", CultureInfo.InvariantCulture)} dBm";
            });
    }
}
