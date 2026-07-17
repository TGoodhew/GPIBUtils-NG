using System;
using System.ComponentModel;
using System.Globalization;
using GpibUtils.Instruments.Counters;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>Shared options for every <c>hp5343a</c> subcommand.</summary>
    public class Hp5343ASettings : InstrumentSettings
    {
        internal Hp5343A OpenDriver(out Visa.IInstrumentSession session)
        {
            session = OpenSession("hp5343a", Hp5343A.DefaultResource);
            return new Hp5343A(session);
        }
    }

    internal static class Hp5343ARunner
    {
        public static int Run(Hp5343ASettings settings, Func<Hp5343A, string> action) => Runner.Guard(() =>
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

    public sealed class Hp5343AIdnCommand : Command<Hp5343AIdnCommand.Settings>
    {
        public sealed class Settings : Hp5343ASettings { }
        public override int Execute(CommandContext context, Settings settings) =>
            Hp5343ARunner.Run(settings, d => d.Identify());
    }

    public sealed class Hp5343AInitCommand : Command<Hp5343AInitCommand.Settings>
    {
        public sealed class Settings : Hp5343ASettings { }
        public override int Execute(CommandContext context, Settings settings) =>
            Hp5343ARunner.Run(settings, d => { d.Initialize(); return null; });
    }

    /// <summary>Measure the input frequency (Hz), optionally in manual mode at a center frequency.</summary>
    public sealed class Hp5343AFreqCommand : Command<Hp5343AFreqCommand.Settings>
    {
        public sealed class Settings : Hp5343ASettings
        {
            [CommandOption("--center <MHZ>")]
            [Description("Manual center frequency in MHz (selects manual mode; default: auto).")]
            public double? CenterMhz { get; set; }

            [CommandOption("--high")]
            [Description("Select the high range (500 MHz–26.5 GHz); default is low range.")]
            public bool HighRange { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp5343ARunner.Run(settings, d =>
            {
                if (settings.HighRange) d.SetHighRange();
                if (settings.CenterMhz.HasValue)
                {
                    d.SetManualMode();
                    d.SetManualCenterFrequencyMHz(settings.CenterMhz.Value);
                }
                else
                {
                    d.SetAutoMode();
                }
                return $"{d.ReadFrequency().ToString("G12", CultureInfo.InvariantCulture)} Hz";
            });
    }
}
