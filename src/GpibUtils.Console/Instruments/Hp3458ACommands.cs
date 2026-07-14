using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using GpibUtils.Instruments.Meters;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>Shared options for every <c>hp3458a</c> subcommand.</summary>
    public class Hp3458ASettings : InstrumentSettings
    {
        internal Hp3458A OpenDriver(out Visa.IInstrumentSession session)
        {
            session = OpenSession("hp3458a", Hp3458A.DefaultResource);
            return new Hp3458A(session);
        }
    }

    internal static class Hp3458AFunctionParser
    {
        private static readonly Dictionary<string, Hp3458AFunction> Map =
            new Dictionary<string, Hp3458AFunction>(StringComparer.OrdinalIgnoreCase)
            {
                ["dcv"] = Hp3458AFunction.DcVoltage,
                ["acv"] = Hp3458AFunction.AcVoltage,
                ["ohm"] = Hp3458AFunction.Resistance2Wire,
                ["ohmf"] = Hp3458AFunction.Resistance4Wire,
                ["dci"] = Hp3458AFunction.DcCurrent,
                ["aci"] = Hp3458AFunction.AcCurrent,
                ["freq"] = Hp3458AFunction.Frequency,
                ["per"] = Hp3458AFunction.Period,
            };

        public static string Choices => string.Join(", ", Map.Keys);

        public static Hp3458AFunction Parse(string name)
        {
            if (name != null && Map.TryGetValue(name.Trim(), out var fn)) return fn;
            throw new ArgumentException($"Unknown function '{name}'. Use one of: {Choices}.");
        }
    }

    internal static class Hp3458ARunner
    {
        public static int Run(Hp3458ASettings settings, Func<Hp3458A, string> action) => Runner.Guard(() =>
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

    public sealed class Hp3458AIdnCommand : Command<Hp3458AIdnCommand.Settings>
    {
        public sealed class Settings : Hp3458ASettings { }
        public override int Execute(CommandContext context, Settings settings) =>
            Hp3458ARunner.Run(settings, d => d.Identify());
    }

    public sealed class Hp3458AInitCommand : Command<Hp3458AInitCommand.Settings>
    {
        public sealed class Settings : Hp3458ASettings { }
        public override int Execute(CommandContext context, Settings settings) =>
            Hp3458ARunner.Run(settings, d => { d.Initialize(); return null; });
    }

    /// <summary>Configure a function then take a reading (or a burst).</summary>
    public sealed class Hp3458AMeasureCommand : Command<Hp3458AMeasureCommand.Settings>
    {
        public sealed class Settings : Hp3458ASettings
        {
            [CommandArgument(0, "<function>")]
            [Description("Function: dcv, acv, ohm, ohmf, dci, aci, freq, per.")]
            public string Function { get; set; }

            [CommandOption("--nplc <NPLC>")]
            [Description("Integration time in power-line cycles.")]
            public double? Nplc { get; set; }

            [CommandOption("-n|--count <N>")]
            [Description("Number of readings to take (default 1).")]
            public int Count { get; set; } = 1;
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp3458ARunner.Run(settings, d =>
            {
                d.ConfigureFunction(Hp3458AFunctionParser.Parse(settings.Function));
                if (settings.Nplc.HasValue) d.SetNplc(settings.Nplc.Value);
                if (settings.Count <= 1)
                    return d.ReadValue().ToString("G9", CultureInfo.InvariantCulture);
                var values = d.ReadValues(settings.Count);
                var s = DmmStatistics.Of(values);
                return string.Format(CultureInfo.InvariantCulture, "n={0} avg={1:G9} min={2:G9} max={3:G9}",
                    s.Count, s.Average, s.Min, s.Max);
            });
    }
}
