using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using GpibUtils.Instruments.Meters;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>Shared options for every <c>hp34401a</c> subcommand (the standard instrument options).</summary>
    public class Hp34401ASettings : InstrumentSettings
    {
        /// <summary>Opens a session (default GPIB0::22::INSTR) and builds the driver.</summary>
        internal Hp34401A OpenDriver(out Visa.IInstrumentSession session)
        {
            session = OpenSession("hp34401a", Hp34401A.DefaultResource);
            return new Hp34401A(session);
        }
    }

    /// <summary>Maps a friendly CLI function name (dcv, acv, res, …) to a <see cref="MeasurementFunction"/>.</summary>
    internal static class DmmFunctionParser
    {
        private static readonly Dictionary<string, MeasurementFunction> Map =
            new Dictionary<string, MeasurementFunction>(StringComparer.OrdinalIgnoreCase)
            {
                ["dcv"] = MeasurementFunction.DcVoltage,
                ["acv"] = MeasurementFunction.AcVoltage,
                ["dci"] = MeasurementFunction.DcCurrent,
                ["aci"] = MeasurementFunction.AcCurrent,
                ["res"] = MeasurementFunction.Resistance2Wire,
                ["fres"] = MeasurementFunction.Resistance4Wire,
                ["freq"] = MeasurementFunction.Frequency,
                ["per"] = MeasurementFunction.Period,
                ["cont"] = MeasurementFunction.Continuity,
                ["diode"] = MeasurementFunction.Diode,
            };

        public static string Choices => string.Join(", ", Map.Keys);

        public static MeasurementFunction Parse(string name)
        {
            if (name != null && Map.TryGetValue(name.Trim(), out var fn)) return fn;
            throw new ArgumentException($"Unknown function '{name}'. Use one of: {Choices}.");
        }
    }

    /// <summary>Shared execution shell: open, run, echo the SCPI sent, and (optionally) print a result.</summary>
    internal static class Hp34401ARunner
    {
        public static int Run(Hp34401ASettings settings, Func<Hp34401A, string> action) => Runner.Guard(() =>
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

    /// <summary>Query the instrument identity (*IDN?).</summary>
    public sealed class Hp34401AIdnCommand : Command<Hp34401AIdnCommand.Settings>
    {
        public sealed class Settings : Hp34401ASettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp34401ARunner.Run(settings, d => d.Identify());
    }

    /// <summary>Device clear + *RST + *CLS (clean known state).</summary>
    public sealed class Hp34401AInitCommand : Command<Hp34401AInitCommand.Settings>
    {
        public sealed class Settings : Hp34401ASettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp34401ARunner.Run(settings, d => { d.Initialize(); return null; });
    }

    /// <summary>Instrument reset (*RST).</summary>
    public sealed class Hp34401AResetCommand : Command<Hp34401AResetCommand.Settings>
    {
        public sealed class Settings : Hp34401ASettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp34401ARunner.Run(settings, d => { d.Reset(); return null; });
    }

    /// <summary>Read a single value from the current configuration (READ?).</summary>
    public sealed class Hp34401AReadCommand : Command<Hp34401AReadCommand.Settings>
    {
        public sealed class Settings : Hp34401ASettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp34401ARunner.Run(settings, d => d.ReadValue().ToString("G9", CultureInfo.InvariantCulture));
    }

    /// <summary>Configure a function then read a single value.</summary>
    public sealed class Hp34401AMeasureCommand : Command<Hp34401AMeasureCommand.Settings>
    {
        public sealed class Settings : Hp34401ASettings
        {
            [CommandArgument(0, "<function>")]
            [Description("Measurement function: dcv, acv, dci, aci, res, fres, freq, per, cont, diode.")]
            public string Function { get; set; }

            [CommandOption("--range <RANGE>")]
            [Description("Range (numeric or MIN/MAX/DEF); default = autorange.")]
            public string Range { get; set; }

            [CommandOption("--resolution <RES>")]
            [Description("Resolution (numeric or MIN/MAX/DEF); default = instrument default.")]
            public string Resolution { get; set; }

            [CommandOption("--nplc <NPLC>")]
            [Description("Integration time in power-line cycles (rangeable functions only).")]
            public double? Nplc { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp34401ARunner.Run(settings, d =>
            {
                var fn = DmmFunctionParser.Parse(settings.Function);
                d.Configure(fn, settings.Range, settings.Resolution);
                if (settings.Nplc.HasValue) d.SetNplc(fn, settings.Nplc.Value);
                return d.ReadValue().ToString("G9", CultureInfo.InvariantCulture);
            });
    }

    /// <summary>Configure a function, take a burst of readings, and report the statistics.</summary>
    public sealed class Hp34401AStatsCommand : Command<Hp34401AStatsCommand.Settings>
    {
        public sealed class Settings : Hp34401ASettings
        {
            [CommandArgument(0, "<function>")]
            [Description("Measurement function: dcv, acv, dci, aci, res, fres, freq, per, cont, diode.")]
            public string Function { get; set; }

            [CommandOption("-n|--count <N>")]
            [Description("Number of samples to take (default 100).")]
            public int Count { get; set; } = 100;

            [CommandOption("--range <RANGE>")]
            [Description("Range (numeric or MIN/MAX/DEF); default = autorange.")]
            public string Range { get; set; }

            [CommandOption("--nplc <NPLC>")]
            [Description("Integration time in power-line cycles (rangeable functions only).")]
            public double? Nplc { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp34401ARunner.Run(settings, d =>
            {
                var fn = DmmFunctionParser.Parse(settings.Function);
                d.Configure(fn, settings.Range);
                if (settings.Nplc.HasValue) d.SetNplc(fn, settings.Nplc.Value);
                var values = d.ReadValues(settings.Count);
                var s = DmmStatistics.Of(values);
                return string.Format(CultureInfo.InvariantCulture,
                    "n={0}  min={1:G7}  max={2:G7}  avg={3:G7}  sd={4:G7}",
                    s.Count, s.Min, s.Max, s.Average, s.StdDev);
            });
    }

    /// <summary>Run the internal self-test (*TST?).</summary>
    public sealed class Hp34401ASelfTestCommand : Command<Hp34401ASelfTestCommand.Settings>
    {
        public sealed class Settings : Hp34401ASettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp34401ARunner.Run(settings, d => d.SelfTest() ? "self-test: PASS" : "self-test: FAIL");
    }

    /// <summary>Drain and print the error queue (SYSTem:ERRor?).</summary>
    public sealed class Hp34401AErrorsCommand : Command<Hp34401AErrorsCommand.Settings>
    {
        public sealed class Settings : Hp34401ASettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp34401ARunner.Run(settings, d =>
            {
                var errors = d.DrainErrors();
                return errors.Count == 0 ? "no errors" : string.Join("\n", errors);
            });
    }

    /// <summary>Set or clear the front-panel display text.</summary>
    public sealed class Hp34401ADisplayCommand : Command<Hp34401ADisplayCommand.Settings>
    {
        public sealed class Settings : Hp34401ASettings
        {
            [CommandArgument(0, "[text]")]
            [Description("Text to show (max 12 chars). Omit with --clear to clear the display text.")]
            public string Text { get; set; }

            [CommandOption("--clear")]
            [Description("Clear the display text instead of setting it.")]
            public bool Clear { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp34401ARunner.Run(settings, d =>
            {
                if (settings.Clear || string.IsNullOrEmpty(settings.Text)) d.ClearDisplayText();
                else d.SetDisplayText(settings.Text);
                return null;
            });
    }
}
