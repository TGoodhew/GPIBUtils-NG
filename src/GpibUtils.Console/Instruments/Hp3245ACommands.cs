using System;
using System.ComponentModel;
using System.Globalization;
using GpibUtils.Instruments.SignalSources;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>Shared options for every <c>hp3245a</c> subcommand.</summary>
    public class Hp3245ASettings : InstrumentSettings
    {
        internal Hp3245A OpenDriver(out Visa.IInstrumentSession session)
        {
            session = OpenSession("hp3245a", Hp3245A.DefaultResource);
            return new Hp3245A(session);
        }
    }

    internal static class Hp3245ARunner
    {
        public static int Run(Hp3245ASettings settings, Func<Hp3245A, string> action) => Runner.Guard(() =>
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

    public sealed class Hp3245AIdnCommand : Command<Hp3245AIdnCommand.Settings>
    {
        public sealed class Settings : Hp3245ASettings { }
        public override int Execute(CommandContext context, Settings settings) =>
            Hp3245ARunner.Run(settings, d => d.Identify());
    }

    /// <summary>Set a DC voltage or current output on channel A or B, optionally reading it back.</summary>
    public sealed class Hp3245ADcCommand : Command<Hp3245ADcCommand.Settings>
    {
        public sealed class Settings : Hp3245ASettings
        {
            [CommandOption("-v|--volts <VOLTS>")]
            [Description("DC voltage output (±10.25 V).")]
            public double? Volts { get; set; }

            [CommandOption("-i|--amps <AMPS>")]
            [Description("DC current output (±0.1 A).")]
            public double? Amps { get; set; }

            [CommandOption("-b|--channel-b")]
            [Description("Use Channel B (default: Channel A).")]
            public bool ChannelB { get; set; }

            [CommandOption("--read")]
            [Description("Read back the programmed output level after setting.")]
            public bool ReadBack { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp3245ARunner.Run(settings, d =>
            {
                d.SelectChannel(settings.ChannelB ? UniversalSourceChannel.ChannelB : UniversalSourceChannel.ChannelA);
                if (settings.Volts.HasValue) d.SetDcVoltage(settings.Volts.Value);
                if (settings.Amps.HasValue) d.SetDcCurrent(settings.Amps.Value);
                return settings.ReadBack
                    ? $"{d.ReadOutput().ToString("G6", CultureInfo.InvariantCulture)}"
                    : null;
            });
    }
}
