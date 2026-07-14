using System;
using System.ComponentModel;
using System.Globalization;
using GpibUtils.Instruments.PowerSupplies;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>Shared options for every <c>hpe3633a</c> subcommand (the standard instrument options).</summary>
    public class HpE3633ASettings : InstrumentSettings
    {
        internal HpE3633A OpenDriver(out Visa.IInstrumentSession session)
        {
            session = OpenSession("hpe3633a", HpE3633A.DefaultResource);
            return new HpE3633A(session);
        }
    }

    /// <summary>Shared execution shell: open, run, echo the SCPI sent, and (optionally) print a result.</summary>
    internal static class HpE3633ARunner
    {
        public static int Run(HpE3633ASettings settings, Func<HpE3633A, string> action) => Runner.Guard(() =>
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
    public sealed class HpE3633AIdnCommand : Command<HpE3633AIdnCommand.Settings>
    {
        public sealed class Settings : HpE3633ASettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            HpE3633ARunner.Run(settings, d => d.Identify());
    }

    /// <summary>Device clear + *RST + *CLS (clean known state).</summary>
    public sealed class HpE3633AInitCommand : Command<HpE3633AInitCommand.Settings>
    {
        public sealed class Settings : HpE3633ASettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            HpE3633ARunner.Run(settings, d => { d.Initialize(); return null; });
    }

    /// <summary>Set output voltage and current limit, then optionally enable the output.</summary>
    public sealed class HpE3633ASetCommand : Command<HpE3633ASetCommand.Settings>
    {
        public sealed class Settings : HpE3633ASettings
        {
            [CommandArgument(0, "<volts>")]
            [Description("Output voltage in volts.")]
            public double Volts { get; set; }

            [CommandOption("-i|--current <AMPS>")]
            [Description("Current limit in amps (default: leave unchanged).")]
            public double? Amps { get; set; }

            [CommandOption("--on")]
            [Description("Enable the output after setting.")]
            public bool On { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            HpE3633ARunner.Run(settings, d =>
            {
                d.SetVoltage(settings.Volts);
                if (settings.Amps.HasValue) d.SetCurrentLimit(settings.Amps.Value);
                if (settings.On) d.SetOutput(true);
                return null;
            });
    }

    /// <summary>Enable or disable the output.</summary>
    public sealed class HpE3633AOutputCommand : Command<HpE3633AOutputCommand.Settings>
    {
        public sealed class Settings : HpE3633ASettings
        {
            [CommandArgument(0, "<state>")]
            [Description("on or off.")]
            public string State { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            HpE3633ARunner.Run(settings, d =>
            {
                bool on = settings.State != null && settings.State.Trim().Equals("on", StringComparison.OrdinalIgnoreCase);
                d.SetOutput(on);
                return null;
            });
    }

    /// <summary>Measure the actual output voltage and current.</summary>
    public sealed class HpE3633AMeasureCommand : Command<HpE3633AMeasureCommand.Settings>
    {
        public sealed class Settings : HpE3633ASettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            HpE3633ARunner.Run(settings, d =>
            {
                double v = d.MeasureVoltage();
                double i = d.MeasureCurrent();
                return $"{v.ToString("G6", CultureInfo.InvariantCulture)} V, {i.ToString("G6", CultureInfo.InvariantCulture)} A";
            });
    }
}
