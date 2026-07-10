using System;
using System.ComponentModel;
using GpibUtils.Instruments.Meters;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>Shared options for every <c>hp8902a</c> subcommand (the standard instrument options).</summary>
    public class Hp8902ASettings : InstrumentSettings
    {
        /// <summary>Opens a session (default GPIB0::14::INSTR) and builds the driver.</summary>
        internal Hp8902A OpenDriver(out Visa.IInstrumentSession session)
        {
            session = OpenSession("hp8902a", Hp8902A.DefaultResource);
            return new Hp8902A(session);
        }
    }

    /// <summary>Options shared by the measure-at-a-frequency subcommands (direct or converted regime).</summary>
    public class Hp8902AMeasureSettings : Hp8902ASettings
    {
        [CommandArgument(0, "<mhz>")]
        [Description("RF frequency to measure, in MHz.")]
        public double Mhz { get; set; }

        [CommandOption("--converted")]
        [Description("Measure through a converter (Frequency-Offset mode); requires --lo.")]
        public bool Converted { get; set; }

        [CommandOption("--lo <MHZ>")]
        [Description("External LO frequency in MHz (converter/offset regime).")]
        public double Lo { get; set; }

        internal MeasurementRegime Regime => Converted ? MeasurementRegime.Converted : MeasurementRegime.Direct;
    }

    /// <summary>Shared execution shell: open, run, echo the mnemonics sent, and (optionally) print a result.</summary>
    internal static class Hp8902ARunner
    {
        public static int Run(Hp8902ASettings settings, Func<Hp8902A, string> action) => Runner.Guard(() =>
        {
            var driver = settings.OpenDriver(out var session);
            using (session)
            {
                string result;
                try
                {
                    result = action(driver);
                }
                catch (Hp8902AException ex)
                {
                    EchoHistory(driver);
                    AnsiConsole.MarkupLineInterpolated($"[red]8902A:[/] {ex.Message}");
                    return 1;
                }
                EchoHistory(driver);
                if (!string.IsNullOrEmpty(result))
                    AnsiConsole.MarkupLineInterpolated($"[green]{result}[/]");
            }
            return 0;
        });

        private static void EchoHistory(Hp8902A driver)
        {
            foreach (var sent in driver.History)
                AnsiConsole.MarkupLineInterpolated($"[grey]sent[/]: [green]{sent}[/]");
        }
    }

    /// <summary>Device clear + preset to a clean known state.</summary>
    public sealed class Hp8902AInitCommand : Command<Hp8902AInitCommand.Settings>
    {
        public sealed class Settings : Hp8902ASettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp8902ARunner.Run(settings, d => { d.Initialize(); return null; });
    }

    /// <summary>Instrument preset (IP).</summary>
    public sealed class Hp8902APresetCommand : Command<Hp8902APresetCommand.Settings>
    {
        public sealed class Settings : Hp8902ASettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp8902ARunner.Run(settings, d => { d.Reset(); return null; });
    }

    /// <summary>Serial-poll the status byte and print it.</summary>
    public sealed class Hp8902AStatusCommand : Command<Hp8902AStatusCommand.Settings>
    {
        public sealed class Settings : Hp8902ASettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp8902ARunner.Run(settings, d =>
            {
                int sb = d.PollStatusByte();
                return sb < 0 ? "status byte: unreadable (poll failed)" : $"status byte: 0x{sb:X2}";
            });
    }

    /// <summary>Measure the input signal frequency (MHz) — a signal-presence check.</summary>
    public sealed class Hp8902AFrequencyCommand : Command<Hp8902AFrequencyCommand.Settings>
    {
        public sealed class Settings : Hp8902ASettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp8902ARunner.Run(settings, d => $"{d.ReadSignalFrequencyMHz():0.######} MHz");
    }

    /// <summary>Measure absolute RF power (dBm) at a frequency.</summary>
    public sealed class Hp8902APowerCommand : Command<Hp8902APowerCommand.Settings>
    {
        public sealed class Settings : Hp8902AMeasureSettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp8902ARunner.Run(settings, d =>
            {
                d.BeginRfPowerMeasurement(settings.Mhz, settings.Regime, settings.Lo);
                return $"{d.ReadRfPowerDbm():0.###} dBm";
            });
    }

    /// <summary>Measure the absolute Tuned RF Level (dBm) at a frequency.</summary>
    public sealed class Hp8902ALevelCommand : Command<Hp8902ALevelCommand.Settings>
    {
        public sealed class Settings : Hp8902AMeasureSettings
        {
            [CommandOption("--sync")]
            [Description("Use the synchronous IF detector (4.0SP, narrow band) instead of average (4.4SP).")]
            public bool Sync { get; set; }

            [CommandOption("--track")]
            [Description("Use Track Mode (32.9SP) to hold lock on a drifting converted signal.")]
            public bool Track { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp8902ARunner.Run(settings, d =>
            {
                d.BeginAttenuationMeasurement(settings.Mhz, settings.Regime, settings.Lo,
                    settings.Sync ? TrflDetector.Synchronous : TrflDetector.Average, settings.Track);
                return $"{d.ReadTunedLevelDbm():0.###} dBm";
            });
    }
}
