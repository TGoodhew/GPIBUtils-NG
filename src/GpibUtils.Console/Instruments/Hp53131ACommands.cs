using System;
using System.ComponentModel;
using System.Globalization;
using GpibUtils.Instruments.Counters;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>Shared options for every <c>hp53131a</c> subcommand (the standard instrument options).</summary>
    public class Hp53131ASettings : InstrumentSettings
    {
        /// <summary>Opens a session (default GPIB0::3::INSTR) and builds the driver.</summary>
        internal Hp53131A OpenDriver(out Visa.IInstrumentSession session)
        {
            session = OpenSession("hp53131a", Hp53131A.DefaultResource);
            return new Hp53131A(session);
        }
    }

    /// <summary>Shared execution shell: open, run, echo the commands sent, and (optionally) print a result.</summary>
    internal static class Hp53131ARunner
    {
        public static int Run(Hp53131ASettings settings, Func<Hp53131A, string> action) => Runner.Guard(() =>
        {
            var driver = settings.OpenDriver(out var session);
            using (session)
            {
                string result;
                try
                {
                    result = action(driver);
                }
                catch (Hp53131AException ex)
                {
                    EchoHistory(driver);
                    AnsiConsole.MarkupLineInterpolated($"[red]53131A:[/] {ex.Message}");
                    return 1;
                }
                EchoHistory(driver);
                if (!string.IsNullOrEmpty(result))
                    AnsiConsole.MarkupLineInterpolated($"[green]{result}[/]");
            }
            return 0;
        });

        private static void EchoHistory(Hp53131A driver)
        {
            foreach (var sent in driver.History)
                AnsiConsole.MarkupLineInterpolated($"[grey]sent[/]: [green]{sent}[/]");
        }
    }

    /// <summary>Query the instrument identity (*IDN?).</summary>
    public sealed class Hp53131AIdnCommand : Command<Hp53131AIdnCommand.Settings>
    {
        public sealed class Settings : Hp53131ASettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp53131ARunner.Run(settings, d => d.Identify());
    }

    /// <summary>Device clear + reset + status preset (clean known state).</summary>
    public sealed class Hp53131AInitCommand : Command<Hp53131AInitCommand.Settings>
    {
        public sealed class Settings : Hp53131ASettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp53131ARunner.Run(settings, d => { d.Initialize(); return null; });
    }

    /// <summary>Instrument reset (*RST).</summary>
    public sealed class Hp53131AResetCommand : Command<Hp53131AResetCommand.Settings>
    {
        public sealed class Settings : Hp53131ASettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp53131ARunner.Run(settings, d => { d.Reset(); return null; });
    }

    /// <summary>Measure the frequency (Hz) on an input channel.</summary>
    public sealed class Hp53131AFreqCommand : Command<Hp53131AFreqCommand.Settings>
    {
        public sealed class Settings : Hp53131ASettings
        {
            [CommandArgument(0, "[channel]")]
            [Description("Input channel to measure: 1, 2, or 3 (default 1).")]
            public int Channel { get; set; } = 1;

            [CommandOption("--impedance <OHMS>")]
            [Description("Set the input impedance first: 50 or 1M (default: leave unchanged).")]
            public string Impedance { get; set; }

            [CommandOption("--wait <MS>")]
            [Description("Completion-wait backstop in milliseconds (default 20000).")]
            public int Wait { get; set; } = 20000;
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp53131ARunner.Run(settings, d =>
            {
                d.CompletionTimeoutMs = settings.Wait;
                if (!string.IsNullOrWhiteSpace(settings.Impedance))
                    d.SetInputImpedance(ParseImpedance(settings.Impedance));
                double hz = d.MeasureFrequency(settings.Channel);
                return $"{hz.ToString("G9", CultureInfo.InvariantCulture)} Hz";
            });

        private static CounterInputImpedance ParseImpedance(string text)
        {
            var t = text.Trim().ToUpperInvariant().Replace("OHM", "").Replace("Ω", "").Trim();
            if (t == "50") return CounterInputImpedance.Ohms50;
            if (t == "1M" || t == "1E6" || t == "1E+6" || t == "1000000") return CounterInputImpedance.Ohms1M;
            throw new ArgumentException($"Unknown impedance '{text}'. Use 50 or 1M.");
        }
    }
}
