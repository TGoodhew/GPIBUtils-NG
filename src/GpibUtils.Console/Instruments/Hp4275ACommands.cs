using System;
using System.ComponentModel;
using GpibUtils.Instruments.LcrMeters;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>Shared options for every <c>hp4275a</c> subcommand (the standard instrument options).</summary>
    public class Hp4275ASettings : InstrumentSettings
    {
        internal Hp4275A OpenDriver(out Visa.IInstrumentSession session)
        {
            session = OpenSession("hp4275a", Hp4275A.DefaultResource);
            return new Hp4275A(session);
        }
    }

    /// <summary>Shared execution shell: open, run, echo the commands sent, and (optionally) print a result.</summary>
    internal static class Hp4275ARunner
    {
        public static int Run(Hp4275ASettings settings, Func<Hp4275A, string> action) => Runner.Guard(() =>
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

    /// <summary>Show the instrument descriptor (4275A has no *IDN?).</summary>
    public sealed class Hp4275AIdnCommand : Command<Hp4275AIdnCommand.Settings>
    {
        public sealed class Settings : Hp4275ASettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp4275ARunner.Run(settings, d => d.Identify());
    }

    /// <summary>Device clear + HOLD/MANUAL trigger (clean known state).</summary>
    public sealed class Hp4275AInitCommand : Command<Hp4275AInitCommand.Settings>
    {
        public sealed class Settings : Hp4275ASettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp4275ARunner.Run(settings, d => { d.Initialize(); return null; });
    }

    /// <summary>Configure parameter/frequency/circuit and take one triggered measurement (SRQ handshake).</summary>
    public sealed class Hp4275AMeasureCommand : Command<Hp4275AMeasureCommand.Settings>
    {
        public sealed class Settings : Hp4275ASettings
        {
            [CommandOption("-p|--parameter <PARAM>")]
            [Description("Primary parameter: l | c | r | z (default c).")]
            public string Parameter { get; set; } = "c";

            [CommandOption("-f|--frequency <FREQ>")]
            [Description("Test frequency: 10k|20k|40k|100k|200k|400k|1M|2M|4M|10M (default 100k).")]
            public string Frequency { get; set; } = "100k";

            [CommandOption("-m|--mode <MODE>")]
            [Description("Circuit mode: auto | series | parallel (default auto).")]
            public string Mode { get; set; } = "auto";
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp4275ARunner.Run(settings, d =>
            {
                d.SetPrimaryParameter(ParseParameter(settings.Parameter));
                d.SetTestFrequency(ParseFrequency(settings.Frequency));
                d.SetCircuitMode(ParseMode(settings.Mode));
                var r = d.Measure();
                return $"primary {r.Primary:G6}, secondary {r.Secondary:G6}";
            });

        private static LcrParameter ParseParameter(string s)
        {
            switch ((s ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "l": return LcrParameter.Inductance;
                case "c": return LcrParameter.Capacitance;
                case "r": return LcrParameter.Resistance;
                case "z": return LcrParameter.ImpedanceMagnitude;
                default: throw new ArgumentException($"Unknown parameter '{s}'.");
            }
        }

        private static LcrCircuitMode ParseMode(string s)
        {
            switch ((s ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "auto": return LcrCircuitMode.Auto;
                case "series": return LcrCircuitMode.Series;
                case "parallel": return LcrCircuitMode.Parallel;
                default: throw new ArgumentException($"Unknown mode '{s}'.");
            }
        }

        private static LcrFrequency ParseFrequency(string s)
        {
            switch ((s ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "10k": return LcrFrequency.F10kHz;
                case "20k": return LcrFrequency.F20kHz;
                case "40k": return LcrFrequency.F40kHz;
                case "100k": return LcrFrequency.F100kHz;
                case "200k": return LcrFrequency.F200kHz;
                case "400k": return LcrFrequency.F400kHz;
                case "1m": return LcrFrequency.F1MHz;
                case "2m": return LcrFrequency.F2MHz;
                case "4m": return LcrFrequency.F4MHz;
                case "10m": return LcrFrequency.F10MHz;
                default: throw new ArgumentException($"Unknown frequency '{s}'.");
            }
        }
    }

    /// <summary>OPEN (zero) correction.</summary>
    public sealed class Hp4275AZeroOpenCommand : Command<Hp4275AZeroOpenCommand.Settings>
    {
        public sealed class Settings : Hp4275ASettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp4275ARunner.Run(settings, d => { d.ZeroOpen(); return null; });
    }
}
