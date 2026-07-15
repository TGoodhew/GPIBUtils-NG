using System;
using System.ComponentModel;
using GpibUtils.Instruments.SignalSources;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>Shared options for every <c>e4438c</c> subcommand (the standard instrument options).</summary>
    public class KeysightE4438CSettings : InstrumentSettings
    {
        /// <summary>Opens a session (default GPIB0::19::INSTR) and builds the driver.</summary>
        internal KeysightE4438C OpenDriver(out Visa.IInstrumentSession session)
        {
            session = OpenSession("e4438c", KeysightE4438C.DefaultResource);
            return new KeysightE4438C(session);
        }
    }

    /// <summary>Shared execution shell: open, run, echo the commands sent, and (optionally) print a result.</summary>
    internal static class KeysightE4438CRunner
    {
        public static int Run(KeysightE4438CSettings settings, Func<KeysightE4438C, string> action) => Runner.Guard(() =>
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
    public sealed class KeysightE4438CIdnCommand : Command<KeysightE4438CIdnCommand.Settings>
    {
        public sealed class Settings : KeysightE4438CSettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            KeysightE4438CRunner.Run(settings, d => d.Identify());
    }

    /// <summary>Device clear + *RST + *CLS + RF off (clean known state).</summary>
    public sealed class KeysightE4438CInitCommand : Command<KeysightE4438CInitCommand.Settings>
    {
        public sealed class Settings : KeysightE4438CSettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            KeysightE4438CRunner.Run(settings, d => { d.Initialize(); return null; });
    }

    /// <summary>Set frequency (MHz) + power (dBm), then turn the RF output on (a one-shot CW setup).</summary>
    public sealed class KeysightE4438CCwCommand : Command<KeysightE4438CCwCommand.Settings>
    {
        public sealed class Settings : KeysightE4438CSettings
        {
            [CommandArgument(0, "<frequency_mhz>")]
            [Description("CW carrier frequency in MHz.")]
            public double FrequencyMHz { get; set; }

            [CommandArgument(1, "<power_dbm>")]
            [Description("Output power in dBm.")]
            public double PowerDbm { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            KeysightE4438CRunner.Run(settings, d =>
            {
                d.SetFrequencyMHz(settings.FrequencyMHz);
                d.SetPowerDbm(settings.PowerDbm);
                d.RfOn();
                return $"CW {settings.FrequencyMHz} MHz @ {settings.PowerDbm} dBm, RF on";
            });
    }

    /// <summary>Set the CW carrier frequency (MHz).</summary>
    public sealed class KeysightE4438CFreqCommand : Command<KeysightE4438CFreqCommand.Settings>
    {
        public sealed class Settings : KeysightE4438CSettings
        {
            [CommandArgument(0, "<frequency_mhz>")]
            [Description("CW carrier frequency in MHz.")]
            public double FrequencyMHz { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            KeysightE4438CRunner.Run(settings, d => { d.SetFrequencyMHz(settings.FrequencyMHz); return $"frequency = {settings.FrequencyMHz} MHz"; });
    }

    /// <summary>Set the output power (dBm).</summary>
    public sealed class KeysightE4438CPowerCommand : Command<KeysightE4438CPowerCommand.Settings>
    {
        public sealed class Settings : KeysightE4438CSettings
        {
            [CommandArgument(0, "<power_dbm>")]
            [Description("Output power in dBm.")]
            public double PowerDbm { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            KeysightE4438CRunner.Run(settings, d => { d.SetPowerDbm(settings.PowerDbm); return $"power = {settings.PowerDbm} dBm"; });
    }

    /// <summary>Turn the RF output on or off.</summary>
    public sealed class KeysightE4438CRfCommand : Command<KeysightE4438CRfCommand.Settings>
    {
        public sealed class Settings : KeysightE4438CSettings
        {
            [CommandArgument(0, "<state>")]
            [Description("RF output state: on or off.")]
            public string State { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            KeysightE4438CRunner.Run(settings, d =>
            {
                bool on = ParseOnOff(settings.State);
                d.SetRfOutput(on);
                return $"RF output {(on ? "on" : "off")}";
            });

        private static bool ParseOnOff(string s)
        {
            switch ((s ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "on": case "1": case "true": return true;
                case "off": case "0": case "false": return false;
                default: throw new ArgumentException($"Unknown state '{s}'. Use on or off.");
            }
        }
    }

    /// <summary>Enable or disable all modulation.</summary>
    public sealed class KeysightE4438CModCommand : Command<KeysightE4438CModCommand.Settings>
    {
        public sealed class Settings : KeysightE4438CSettings
        {
            [CommandArgument(0, "<state>")]
            [Description("Modulation state: on or off.")]
            public string State { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            KeysightE4438CRunner.Run(settings, d =>
            {
                bool on = (settings.State ?? "").Trim().ToLowerInvariant() is "on" or "1" or "true";
                d.SetModulation(on);
                return $"modulation {(on ? "on" : "off")}";
            });
    }

    /// <summary>Read the head of the SCPI error queue (:SYSTem:ERRor?).</summary>
    public sealed class KeysightE4438CErrorCommand : Command<KeysightE4438CErrorCommand.Settings>
    {
        public sealed class Settings : KeysightE4438CSettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            KeysightE4438CRunner.Run(settings, d => $"error: {d.GetError()}");
    }
}
