using System;
using System.ComponentModel;
using GpibUtils.Instruments.SignalSources;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>Shared options for every <c>hp3335a</c> subcommand.</summary>
    public class Hp3335ASettings : InstrumentSettings
    {
        internal Hp3335A OpenDriver(out Visa.IInstrumentSession session)
        {
            session = OpenSession("hp3335a", Hp3335A.DefaultResource);
            return new Hp3335A(session);
        }
    }

    internal static class Hp3335ARunner
    {
        public static int Run(Hp3335ASettings settings, Func<Hp3335A, string> action) => Runner.Guard(() =>
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

    public sealed class Hp3335AIdnCommand : Command<Hp3335AIdnCommand.Settings>
    {
        public sealed class Settings : Hp3335ASettings { }
        public override int Execute(CommandContext context, Settings settings) =>
            Hp3335ARunner.Run(settings, d => d.Identify());
    }

    /// <summary>Set the output frequency (MHz) and/or amplitude (dBm) — the 3335A is listen-only.</summary>
    public sealed class Hp3335ASetCommand : Command<Hp3335ASetCommand.Settings>
    {
        public sealed class Settings : Hp3335ASettings
        {
            [CommandOption("-f|--frequency <MHZ>")]
            [Description("Output frequency, in MHz.")]
            public double? FrequencyMHz { get; set; }

            [CommandOption("-l|--level <DBM>")]
            [Description("Output amplitude, in dBm.")]
            public double? LevelDbm { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp3335ARunner.Run(settings, d =>
            {
                if (settings.FrequencyMHz.HasValue) d.SetFrequencyMHz(settings.FrequencyMHz.Value);
                if (settings.LevelDbm.HasValue) d.SetAmplitudeDbm(settings.LevelDbm.Value);
                return null;
            });
    }
}
