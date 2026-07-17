using System;
using System.ComponentModel;
using GpibUtils.Instruments.Meters;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    public sealed class Hp8901IdnCommand : Command<Hp8901IdnCommand.Settings>
    {
        public sealed class Settings : InstrumentSettings { }

        public override int Execute(CommandContext context, Settings settings) => Runner.Guard(() =>
        {
            var session = settings.OpenSession("hp8901", Hp8901.DefaultResource);
            using (session) AnsiConsole.MarkupLineInterpolated($"[green]{new Hp8901(session).Identify()}[/]");
            return 0;
        });
    }

    /// <summary>Tune a carrier and measure a modulation/RF quantity.</summary>
    public sealed class Hp8901MeasureCommand : Command<Hp8901MeasureCommand.Settings>
    {
        public sealed class Settings : InstrumentSettings
        {
            [CommandArgument(0, "<QUANTITY>")]
            [Description("am | fm | pm | power | freq.")]
            public string Quantity { get; set; }

            [CommandOption("-f|--frequency <MHZ>")]
            [Description("Carrier frequency to tune to, in MHz.")]
            public double? FrequencyMHz { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) => Runner.Guard(() =>
        {
            var session = settings.OpenSession("hp8901", Hp8901.DefaultResource);
            var d = new Hp8901(session);
            using (session)
            {
                d.Initialize();
                if (settings.FrequencyMHz.HasValue) d.TuneMHz(settings.FrequencyMHz.Value);
                var q = Parse(settings.Quantity);
                double v = d.Measure(q);
                foreach (var sent in d.History) AnsiConsole.MarkupLineInterpolated($"[grey]sent[/]: [green]{sent}[/]");
                AnsiConsole.MarkupLineInterpolated($"[green]{q} = {v}[/]");
            }
            return 0;
        });

        private static ModulationMeasurementType Parse(string s)
        {
            switch ((s ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "am": return ModulationMeasurementType.Am;
                case "fm": return ModulationMeasurementType.Fm;
                case "pm": return ModulationMeasurementType.PhaseModulation;
                case "power": return ModulationMeasurementType.RfPower;
                case "freq": return ModulationMeasurementType.Frequency;
                default: throw new ArgumentException($"Unknown quantity '{s}'.");
            }
        }
    }
}
