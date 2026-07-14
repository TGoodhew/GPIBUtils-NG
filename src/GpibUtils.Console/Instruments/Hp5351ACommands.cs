using System;
using System.Globalization;
using GpibUtils.Instruments.Counters;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>Shared options for every <c>hp5351a</c> subcommand.</summary>
    public class Hp5351ASettings : InstrumentSettings
    {
        internal Hp5351A OpenDriver(out Visa.IInstrumentSession session)
        {
            session = OpenSession("hp5351a", Hp5351A.DefaultResource);
            return new Hp5351A(session);
        }
    }

    internal static class Hp5351ARunner
    {
        public static int Run(Hp5351ASettings settings, Func<Hp5351A, string> action) => Runner.Guard(() =>
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

    public sealed class Hp5351AIdnCommand : Command<Hp5351AIdnCommand.Settings>
    {
        public sealed class Settings : Hp5351ASettings { }
        public override int Execute(CommandContext context, Settings settings) =>
            Hp5351ARunner.Run(settings, d => d.Identify());
    }

    public sealed class Hp5351AInitCommand : Command<Hp5351AInitCommand.Settings>
    {
        public sealed class Settings : Hp5351ASettings { }
        public override int Execute(CommandContext context, Settings settings) =>
            Hp5351ARunner.Run(settings, d => { d.Initialize(); return null; });
    }

    /// <summary>Measure the input frequency (Hz).</summary>
    public sealed class Hp5351AFreqCommand : Command<Hp5351AFreqCommand.Settings>
    {
        public sealed class Settings : Hp5351ASettings { }
        public override int Execute(CommandContext context, Settings settings) =>
            Hp5351ARunner.Run(settings, d =>
            {
                d.SetSampleMode(CounterSampleMode.Hold);
                return $"{d.ReadFrequency().ToString("G12", CultureInfo.InvariantCulture)} Hz";
            });
    }

    /// <summary>Show oven and reference status.</summary>
    public sealed class Hp5351AStatusCommand : Command<Hp5351AStatusCommand.Settings>
    {
        public sealed class Settings : Hp5351ASettings { }
        public override int Execute(CommandContext context, Settings settings) =>
            Hp5351ARunner.Run(settings, d => $"oven: {d.OvenStatus()}, reference: {d.ReferenceSource()}");
    }
}
