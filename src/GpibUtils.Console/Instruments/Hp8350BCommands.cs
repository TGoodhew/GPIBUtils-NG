using System;
using System.ComponentModel;
using GpibUtils.Instruments.SignalSources;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>Shared options for every <c>hp8350b</c> subcommand.</summary>
    public class Hp8350BSettings : InstrumentSettings
    {
        internal Hp8350B OpenDriver(out Visa.IInstrumentSession session)
        {
            session = OpenSession("hp8350b", Hp8350B.DefaultResource);
            return new Hp8350B(session);
        }
    }

    internal static class Hp8350BRunner
    {
        public static int Run(Hp8350BSettings settings, Func<Hp8350B, string> action) => Runner.Guard(() =>
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

    public sealed class Hp8350BPresetCommand : Command<Hp8350BPresetCommand.Settings>
    {
        public sealed class Settings : Hp8350BSettings { }
        public override int Execute(CommandContext context, Settings settings) =>
            Hp8350BRunner.Run(settings, d => { d.Preset(); return null; });
    }

    public sealed class Hp8350BInitCommand : Command<Hp8350BInitCommand.Settings>
    {
        public sealed class Settings : Hp8350BSettings { }
        public override int Execute(CommandContext context, Settings settings) =>
            Hp8350BRunner.Run(settings, d => { d.Initialize(); return null; });
    }

    /// <summary>Set the CW output frequency (MHz).</summary>
    public sealed class Hp8350BFreqCommand : Command<Hp8350BFreqCommand.Settings>
    {
        public sealed class Settings : Hp8350BSettings
        {
            [CommandArgument(0, "<mhz>")]
            [Description("CW frequency in MHz.")]
            public double Mhz { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp8350BRunner.Run(settings, d => { d.SetFrequencyMHz(settings.Mhz); return null; });
    }

    /// <summary>Set the output power (dBm).</summary>
    public sealed class Hp8350BPowerCommand : Command<Hp8350BPowerCommand.Settings>
    {
        public sealed class Settings : Hp8350BSettings
        {
            [CommandArgument(0, "<dbm>")]
            [Description("Output power in dBm.")]
            public double Dbm { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp8350BRunner.Run(settings, d => { d.SetPowerDbm(settings.Dbm); return null; });
    }

    /// <summary>Preset, set frequency + power.</summary>
    public sealed class Hp8350BCwCommand : Command<Hp8350BCwCommand.Settings>
    {
        public sealed class Settings : Hp8350BSettings
        {
            [CommandArgument(0, "<mhz>")]
            [Description("CW frequency in MHz.")]
            public double Mhz { get; set; }

            [CommandArgument(1, "<dbm>")]
            [Description("Output power in dBm.")]
            public double Dbm { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp8350BRunner.Run(settings, d =>
            {
                d.Preset();
                d.SetFrequencyMHz(settings.Mhz);
                d.SetPowerDbm(settings.Dbm);
                return null;
            });
    }
}
