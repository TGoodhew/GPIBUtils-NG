using System;
using System.ComponentModel;
using System.Globalization;
using GpibUtils.Instruments.Counters;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>Shared options for every <c>hp5342a</c> subcommand.</summary>
    public class Hp5342ASettings : InstrumentSettings
    {
        internal Hp5342A OpenDriver(out Visa.IInstrumentSession session)
        {
            session = OpenSession("hp5342a", Hp5342A.DefaultResource);
            return new Hp5342A(session);
        }
    }

    internal static class Hp5342ARunner
    {
        public static int Run(Hp5342ASettings settings, Func<Hp5342A, string> action) => Runner.Guard(() =>
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

    public sealed class Hp5342AIdnCommand : Command<Hp5342AIdnCommand.Settings>
    {
        public sealed class Settings : Hp5342ASettings { }
        public override int Execute(CommandContext context, Settings settings) =>
            Hp5342ARunner.Run(settings, d => d.Identify());
    }

    public sealed class Hp5342AInitCommand : Command<Hp5342AInitCommand.Settings>
    {
        public sealed class Settings : Hp5342ASettings { }
        public override int Execute(CommandContext context, Settings settings) =>
            Hp5342ARunner.Run(settings, d => { d.Initialize(); return null; });
    }

    /// <summary>Measure the input frequency (Hz), optionally in manual mode at a center frequency.</summary>
    public sealed class Hp5342AFreqCommand : Command<Hp5342AFreqCommand.Settings>
    {
        public sealed class Settings : Hp5342ASettings
        {
            [CommandOption("--center <MHZ>")]
            [Description("Manual center frequency in MHz (selects manual mode; default: auto).")]
            public double? CenterMhz { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp5342ARunner.Run(settings, d =>
            {
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
