using System;
using System.ComponentModel;
using GpibUtils.Instruments.Meters;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    public sealed class Hp8508AIdnCommand : Command<Hp8508AIdnCommand.Settings>
    {
        public sealed class Settings : InstrumentSettings { }

        public override int Execute(CommandContext context, Settings settings) => Runner.Guard(() =>
        {
            var session = settings.OpenSession("hp8508a", Hp8508A.DefaultResource);
            using (session) AnsiConsole.MarkupLineInterpolated($"[green]{new Hp8508A(session).Identify()}[/]");
            return 0;
        });
    }

    /// <summary>Measure a vector quantity (auto-band, then MEASure?).</summary>
    public sealed class Hp8508AMeasureCommand : Command<Hp8508AMeasureCommand.Settings>
    {
        public sealed class Settings : InstrumentSettings
        {
            [CommandArgument(0, "<QUANTITY>")]
            [Description("avol | bvol | apow | bpow | ba | phase | trans | delay | swr | rho | y | z.")]
            public string Quantity { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) => Runner.Guard(() =>
        {
            var session = settings.OpenSession("hp8508a", Hp8508A.DefaultResource);
            var d = new Hp8508A(session);
            using (session)
            {
                d.Initialize();
                var q = Parse(settings.Quantity);
                double v = d.Measure(q);
                foreach (var sent in d.History) AnsiConsole.MarkupLineInterpolated($"[grey]sent[/]: [green]{sent}[/]");
                AnsiConsole.MarkupLineInterpolated($"[green]{q} = {v}[/]");
            }
            return 0;
        });

        private static VectorMeasurement Parse(string s)
        {
            switch ((s ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "avol": return VectorMeasurement.ChannelAVoltage;
                case "bvol": return VectorMeasurement.ChannelBVoltage;
                case "apow": return VectorMeasurement.ChannelAPower;
                case "bpow": return VectorMeasurement.ChannelBPower;
                case "ba": return VectorMeasurement.RatioBA;
                case "phase": return VectorMeasurement.Phase;
                case "trans": return VectorMeasurement.Transmission;
                case "delay": return VectorMeasurement.GroupDelay;
                case "swr": return VectorMeasurement.Swr;
                case "rho": return VectorMeasurement.ReflectionCoefficient;
                case "y": return VectorMeasurement.Admittance;
                case "z": return VectorMeasurement.Impedance;
                default: throw new ArgumentException($"Unknown quantity '{s}'.");
            }
        }
    }
}
