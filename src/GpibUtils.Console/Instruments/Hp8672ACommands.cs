using System;
using System.ComponentModel;
using GpibUtils.Instruments.SignalSources;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>Shared options for every <c>hp8672a</c> subcommand (the standard instrument options).</summary>
    public class Hp8672ASettings : InstrumentSettings
    {
        internal Hp8672A OpenDriver(out Visa.IInstrumentSession session)
        {
            session = OpenSession("hp8672a", Hp8672A.DefaultResource);
            return new Hp8672A(session);
        }
    }

    /// <summary>Shared execution shell: open, run, echo the commands sent, and (optionally) print a result.</summary>
    internal static class Hp8672ARunner
    {
        public static int Run(Hp8672ASettings settings, Func<Hp8672A, string> action) => Runner.Guard(() =>
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

    /// <summary>Device clear + RF off (clean known state; device clear resets to 3 GHz).</summary>
    public sealed class Hp8672AInitCommand : Command<Hp8672AInitCommand.Settings>
    {
        public sealed class Settings : Hp8672ASettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp8672ARunner.Run(settings, d => { d.Initialize(); return null; });
    }

    /// <summary>Set frequency (settling for phase lock) + power, and turn the RF on.</summary>
    public sealed class Hp8672ACwCommand : Command<Hp8672ACwCommand.Settings>
    {
        public sealed class Settings : Hp8672ASettings
        {
            [CommandArgument(0, "<MHZ>")]
            [Description("CW frequency in MHz (2000-18000).")]
            public double FrequencyMHz { get; set; }

            [CommandArgument(1, "[DBM]")]
            [Description("Output power in dBm (default -10).")]
            public double PowerDbm { get; set; } = -10;
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp8672ARunner.Run(settings, d =>
            {
                d.SetFrequencyAndSettleMHz(settings.FrequencyMHz);
                d.SetPowerDbm(settings.PowerDbm);
                d.RfOn();
                return $"CW {settings.FrequencyMHz} MHz @ {settings.PowerDbm} dBm, phase-locked, RF on";
            });
    }

    /// <summary>Set the CW frequency and wait for phase lock.</summary>
    public sealed class Hp8672AFreqCommand : Command<Hp8672AFreqCommand.Settings>
    {
        public sealed class Settings : Hp8672ASettings
        {
            [CommandArgument(0, "<MHZ>")]
            [Description("CW frequency in MHz.")]
            public double FrequencyMHz { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp8672ARunner.Run(settings, d => { d.SetFrequencyAndSettleMHz(settings.FrequencyMHz); return "phase-locked"; });
    }

    /// <summary>Set the output power (dBm).</summary>
    public sealed class Hp8672APowerCommand : Command<Hp8672APowerCommand.Settings>
    {
        public sealed class Settings : Hp8672ASettings
        {
            [CommandArgument(0, "<DBM>")]
            [Description("Output power in dBm.")]
            public double PowerDbm { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp8672ARunner.Run(settings, d => { d.SetPowerDbm(settings.PowerDbm); return null; });
    }

    /// <summary>Turn the RF output on or off.</summary>
    public sealed class Hp8672ARfCommand : Command<Hp8672ARfCommand.Settings>
    {
        public sealed class Settings : Hp8672ASettings
        {
            [CommandArgument(0, "<STATE>")]
            [Description("on or off.")]
            public string State { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp8672ARunner.Run(settings, d =>
            {
                bool on = string.Equals(settings.State, "on", StringComparison.OrdinalIgnoreCase);
                if (on) d.RfOn(); else d.RfOff();
                return null;
            });
    }

    /// <summary>Serial-poll and report the status byte (phase-lock / fault bits).</summary>
    public sealed class Hp8672AStatusCommand : Command<Hp8672AStatusCommand.Settings>
    {
        public sealed class Settings : Hp8672ASettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp8672ARunner.Run(settings, d =>
            {
                int stb = d.ReadStatusByte();
                return $"status 0x{stb:X2}; phase-locked: {d.IsPhaseLocked()}";
            });
    }
}
