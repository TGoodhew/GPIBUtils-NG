using System;
using System.ComponentModel;
using System.Globalization;
using GpibUtils.Instruments.ElectronicLoads;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>Shared options for every <c>maynuo</c> subcommand.</summary>
    public class MaynuoSettings : InstrumentSettings
    {
        internal MaynuoM9811 OpenDriver(out Visa.IInstrumentSession session)
        {
            session = OpenSession("maynuo", MaynuoM9811.DefaultResource);
            return new MaynuoM9811(session);
        }
    }

    internal static class MaynuoRunner
    {
        public static int Run(MaynuoSettings settings, Func<MaynuoM9811, string> action) => Runner.Guard(() =>
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

    public sealed class MaynuoIdnCommand : Command<MaynuoIdnCommand.Settings>
    {
        public sealed class Settings : MaynuoSettings { }
        public override int Execute(CommandContext context, Settings settings) =>
            MaynuoRunner.Run(settings, d => d.Identify());
    }

    /// <summary>Put the load in remote control, select a mode + setpoint, and switch the input on/off.</summary>
    public sealed class MaynuoSetCommand : Command<MaynuoSetCommand.Settings>
    {
        public sealed class Settings : MaynuoSettings
        {
            [CommandArgument(0, "<MODE>")]
            [Description("cc | cv | cr | cw.")]
            public string Mode { get; set; }

            [CommandArgument(1, "<SETPOINT>")]
            [Description("Amps (cc) / volts (cv) / ohms (cr) / watts (cw).")]
            public double Setpoint { get; set; }

            [CommandOption("--input <STATE>")]
            [Description("on | off (default: leave unchanged).")]
            public string Input { get; set; }
        }

        public override int Execute(CommandContext context, Settings s) => MaynuoRunner.Run(s, d =>
        {
            d.Initialize();
            var mode = ParseMode(s.Mode);
            d.SetMode(mode, s.Setpoint);
            if (string.Equals(s.Input, "on", StringComparison.OrdinalIgnoreCase)) d.InputOn();
            else if (string.Equals(s.Input, "off", StringComparison.OrdinalIgnoreCase)) d.InputOff();
            return $"{mode} = {s.Setpoint.ToString(CultureInfo.InvariantCulture)}";
        });

        private static LoadMode ParseMode(string m)
        {
            switch ((m ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "cc": return LoadMode.ConstantCurrent;
                case "cv": return LoadMode.ConstantVoltage;
                case "cr": return LoadMode.ConstantResistance;
                case "cw": return LoadMode.ConstantPower;
                default: throw new ArgumentException($"Unknown mode '{m}'. Use cc | cv | cr | cw.");
            }
        }
    }

    /// <summary>Read the load's measured voltage, current and power.</summary>
    public sealed class MaynuoReadCommand : Command<MaynuoReadCommand.Settings>
    {
        public sealed class Settings : MaynuoSettings { }
        public override int Execute(CommandContext context, Settings settings) =>
            MaynuoRunner.Run(settings, d =>
            {
                double v = d.ReadVoltage(), i = d.ReadCurrent();
                return $"{v.ToString("G6", CultureInfo.InvariantCulture)} V, " +
                       $"{i.ToString("G6", CultureInfo.InvariantCulture)} A, " +
                       $"{(v * i).ToString("G6", CultureInfo.InvariantCulture)} W";
            });
    }
}
