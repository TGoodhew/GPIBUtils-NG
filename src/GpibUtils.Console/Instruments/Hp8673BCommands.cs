using System;
using System.ComponentModel;
using GpibUtils.Instruments.SignalSources;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>Shared options for every <c>hp8673b</c> subcommand (the standard instrument options).</summary>
    public class Hp8673BSettings : InstrumentSettings
    {
        /// <summary>Opens a session (default GPIB0::19::INSTR) and builds the driver.</summary>
        internal Hp8673B OpenDriver(out Visa.IInstrumentSession session)
        {
            session = OpenSession("hp8673b", Hp8673B.DefaultResource);
            return new Hp8673B(session);
        }
    }

    /// <summary>Shared execution shell: open, run the driver action, echo the mnemonics sent.</summary>
    internal static class Hp8673BRunner
    {
        public static int Run(Hp8673BSettings settings, Action<Hp8673B> action) => Runner.Guard(() =>
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
    public sealed class Hp8673BPresetCommand : Command<Hp8673BPresetCommand.Settings>
    {
        public sealed class Settings : Hp8673BSettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp8673BRunner.Run(settings, d => d.Preset());
    }

    /// <summary>Device clear + preset + RF off (a clean known state).</summary>
    public sealed class Hp8673BInitCommand : Command<Hp8673BInitCommand.Settings>
    {
        public sealed class Settings : Hp8673BSettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp8673BRunner.Run(settings, d => d.Initialize());
    }

    /// <summary>Sets the output frequency (MHz).</summary>
    public sealed class Hp8673BFreqCommand : Command<Hp8673BFreqCommand.Settings>
    {
        public sealed class Settings : Hp8673BSettings
        {
            [CommandArgument(0, "<mhz>")]
            [Description("Frequency in MHz (8673B range 2000-26500).")]
            public double Mhz { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp8673BRunner.Run(settings, d => d.SetFrequencyMHz(settings.Mhz));
    }

    /// <summary>Sets the output level (dBm).</summary>
    public sealed class Hp8673BPowerCommand : Command<Hp8673BPowerCommand.Settings>
    {
        public sealed class Settings : Hp8673BSettings
        {
            [CommandArgument(0, "<dbm>")]
            [Description("Output level in dBm.")]
            public double Dbm { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp8673BRunner.Run(settings, d => d.SetPowerDbm(settings.Dbm));
    }

    /// <summary>Turns the RF output on or off.</summary>
    public sealed class Hp8673BRfCommand : Command<Hp8673BRfCommand.Settings>
    {
        public sealed class Settings : Hp8673BSettings
        {
            [CommandArgument(0, "<state>")]
            [Description("on = RF1, off = RF0.")]
            public string State { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp8673BRunner.Run(settings, d =>
            {
                if (Hp8673BRunner.ParseOnOff(settings.State)) d.RfOn();
                else d.RfOff();
            });
    }

    /// <summary>One-shot: preset, set frequency + level, and enable the RF output.</summary>
    public sealed class Hp8673BCwCommand : Command<Hp8673BCwCommand.Settings>
    {
        public sealed class Settings : Hp8673BSettings
        {
            [CommandArgument(0, "<mhz>")]
            [Description("Frequency in MHz.")]
            public double Mhz { get; set; }

            [CommandArgument(1, "<dbm>")]
            [Description("Output level in dBm.")]
            public double Dbm { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp8673BRunner.Run(settings, d =>
            {
                d.Initialize();
                d.SetFrequencyMHz(settings.Mhz);
                d.SetPowerDbm(settings.Dbm);
                d.RfOn();
            });
    }
}
