using System;
using System.ComponentModel;
using GpibUtils.Instruments.SourceMeasure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    public sealed class Keithley2400IdnCommand : Command<Keithley2400IdnCommand.Settings>
    {
        public sealed class Settings : InstrumentSettings { }

        public override int Execute(CommandContext context, Settings settings) => Runner.Guard(() =>
        {
            var session = settings.OpenSession("keithley2400", Keithley2400.DefaultResource);
            using (session) AnsiConsole.MarkupLineInterpolated($"[green]{new Keithley2400(session).Identify()}[/]");
            return 0;
        });
    }

    /// <summary>Source a voltage (with current compliance), enable the output, and read V/I/R.</summary>
    public sealed class Keithley2400MeasureCommand : Command<Keithley2400MeasureCommand.Settings>
    {
        public sealed class Settings : InstrumentSettings
        {
            [CommandArgument(0, "<VOLTS>")]
            [Description("Source voltage.")]
            public double Volts { get; set; }

            [CommandOption("-i|--compliance <AMPS>")]
            [Description("Current compliance limit (default 0.1 A).")]
            public double Compliance { get; set; } = 0.1;
        }

        public override int Execute(CommandContext context, Settings settings) => Runner.Guard(() =>
        {
            var session = settings.OpenSession("keithley2400", Keithley2400.DefaultResource);
            var d = new Keithley2400(session);
            using (session)
            {
                d.Initialize();
                d.SetSourceFunction(SmuSourceFunction.Voltage);
                d.SetSourceLevel(settings.Volts);
                d.SetCompliance(settings.Compliance);
                d.SetOutput(true);
                var r = d.Measure();
                foreach (var sent in d.History) AnsiConsole.MarkupLineInterpolated($"[grey]sent[/]: [green]{sent}[/]");
                AnsiConsole.MarkupLineInterpolated($"[green]{r}[/]");
            }
            return 0;
        });
    }
}
