using System;
using System.ComponentModel;
using GpibUtils.Instruments.SignalSources;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>Shared options for every <c>hp8340b</c> subcommand (the standard instrument options).</summary>
    public class Hp8340BSettings : InstrumentSettings
    {
        /// <summary>Opens a session (default GPIB0::20::INSTR) and builds the driver.</summary>
        internal Hp8340B OpenDriver(out Visa.IInstrumentSession session)
        {
            session = OpenSession("hp8340b", Hp8340B.DefaultResource);
            return new Hp8340B(session);
        }
    }

    /// <summary>Shared execution shell: open, run the driver action, echo the mnemonics sent.</summary>
    internal static class Hp8340BRunner
    {
        public static int Run(Hp8340BSettings settings, Action<Hp8340B> action) => Runner.Guard(() =>
        {
            var driver = settings.OpenDriver(out var session);
            using (session)
            {
                action(driver);
                foreach (var sent in driver.History)
                    AnsiConsole.MarkupLineInterpolated($"[grey]sent[/]: [green]{sent}[/]");
            }
            return 0;
        });

        public static bool ParseOnOff(string value)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "on": case "1": case "true": case "yes": return true;
                case "off": case "0": case "false": case "no": return false;
                default: throw new ArgumentException($"Expected on/off (got '{value}').");
            }
        }
    }

    /// <summary>Instrument preset (IP).</summary>
    public sealed class Hp8340BPresetCommand : Command<Hp8340BPresetCommand.Settings>
    {
        public sealed class Settings : Hp8340BSettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp8340BRunner.Run(settings, d => d.Preset());
    }

    /// <summary>Device clear + preset + RF off (a clean known state).</summary>
    public sealed class Hp8340BInitCommand : Command<Hp8340BInitCommand.Settings>
    {
        public sealed class Settings : Hp8340BSettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp8340BRunner.Run(settings, d => d.Initialize());
    }

    /// <summary>Sets the CW output frequency (MHz).</summary>
    public sealed class Hp8340BFreqCommand : Command<Hp8340BFreqCommand.Settings>
    {
        public sealed class Settings : Hp8340BSettings
        {
            [CommandArgument(0, "<mhz>")]
            [Description("CW frequency in MHz (8340B range 0.01-26500).")]
            public double Mhz { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp8340BRunner.Run(settings, d => d.SetFrequencyMHz(settings.Mhz));
    }

    /// <summary>Sets the output power (dBm).</summary>
    public sealed class Hp8340BPowerCommand : Command<Hp8340BPowerCommand.Settings>
    {
        public sealed class Settings : Hp8340BSettings
        {
            [CommandArgument(0, "<dbm>")]
            [Description("Output power in dBm.")]
            public double Dbm { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp8340BRunner.Run(settings, d => d.SetPowerDbm(settings.Dbm));
    }

    /// <summary>Turns the RF output on or off.</summary>
    public sealed class Hp8340BRfCommand : Command<Hp8340BRfCommand.Settings>
    {
        public sealed class Settings : Hp8340BSettings
        {
            [CommandArgument(0, "<state>")]
            [Description("on = RF1, off = RF0.")]
            public string State { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp8340BRunner.Run(settings, d =>
            {
                if (Hp8340BRunner.ParseOnOff(settings.State)) d.RfOn();
                else d.RfOff();
            });
    }

    /// <summary>One-shot: preset, set frequency + power, and enable the RF output.</summary>
    public sealed class Hp8340BCwCommand : Command<Hp8340BCwCommand.Settings>
    {
        public sealed class Settings : Hp8340BSettings
        {
            [CommandArgument(0, "<mhz>")]
            [Description("CW frequency in MHz.")]
            public double Mhz { get; set; }

            [CommandArgument(1, "<dbm>")]
            [Description("Output power in dBm.")]
            public double Dbm { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp8340BRunner.Run(settings, d =>
            {
                d.Initialize();
                d.SetFrequencyMHz(settings.Mhz);
                d.SetPowerDbm(settings.Dbm);
                d.RfOn();
            });
    }
}
