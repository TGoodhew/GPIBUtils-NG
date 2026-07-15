using System;
using System.Collections.Generic;
using System.ComponentModel;
using GpibUtils.Instruments.SignalSources;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>Shared options for every <c>hp3325b</c> subcommand.</summary>
    public class Hp3325BSettings : InstrumentSettings
    {
        internal Hp3325B OpenDriver(out Visa.IInstrumentSession session)
        {
            session = OpenSession("hp3325b", Hp3325B.DefaultResource);
            return new Hp3325B(session);
        }
    }

    internal static class Hp3325BWaveformParser
    {
        private static readonly Dictionary<string, Hp3325BWaveform> Map =
            new Dictionary<string, Hp3325BWaveform>(StringComparer.OrdinalIgnoreCase)
            {
                ["dc"] = Hp3325BWaveform.Dc,
                ["sine"] = Hp3325BWaveform.Sine,
                ["square"] = Hp3325BWaveform.Square,
                ["triangle"] = Hp3325BWaveform.Triangle,
                ["ramp"] = Hp3325BWaveform.PositiveRamp,
            };

        public static string Choices => string.Join(", ", Map.Keys);

        public static Hp3325BWaveform Parse(string name)
        {
            if (name != null && Map.TryGetValue(name.Trim(), out var w)) return w;
            throw new ArgumentException($"Unknown waveform '{name}'. Use one of: {Choices}.");
        }
    }

    internal static class Hp3325BRunner
    {
        public static int Run(Hp3325BSettings settings, Func<Hp3325B, string> action) => Runner.Guard(() =>
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

    public sealed class Hp3325BIdnCommand : Command<Hp3325BIdnCommand.Settings>
    {
        public sealed class Settings : Hp3325BSettings { }
        public override int Execute(CommandContext context, Settings settings) =>
            Hp3325BRunner.Run(settings, d => d.Identify());
    }

    public sealed class Hp3325BInitCommand : Command<Hp3325BInitCommand.Settings>
    {
        public sealed class Settings : Hp3325BSettings { }
        public override int Execute(CommandContext context, Settings settings) =>
            Hp3325BRunner.Run(settings, d => { d.Initialize(); return null; });
    }

    /// <summary>Set waveform, frequency (Hz), amplitude (V), and DC offset (V).</summary>
    public sealed class Hp3325BSetCommand : Command<Hp3325BSetCommand.Settings>
    {
        public sealed class Settings : Hp3325BSettings
        {
            [CommandOption("-w|--waveform <NAME>")]
            [Description("Waveform: dc, sine, square, triangle, ramp.")]
            public string Waveform { get; set; }

            [CommandOption("-f|--freq <HZ>")]
            [Description("Frequency in Hz.")]
            public double? FreqHz { get; set; }

            [CommandOption("-l|--amplitude <V>")]
            [Description("Amplitude in volts.")]
            public double? AmplitudeV { get; set; }

            [CommandOption("-o|--offset <V>")]
            [Description("DC offset in volts.")]
            public double? OffsetV { get; set; }

            [CommandOption("--cal")]
            [Description("Perform an amplitude calibration (AC) after setting.")]
            public bool Cal { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp3325BRunner.Run(settings, d =>
            {
                if (!string.IsNullOrWhiteSpace(settings.Waveform)) d.SetWaveform(Hp3325BWaveformParser.Parse(settings.Waveform));
                if (settings.FreqHz.HasValue) d.SetFrequencyHz(settings.FreqHz.Value);
                if (settings.AmplitudeV.HasValue) d.SetAmplitudeVolts(settings.AmplitudeV.Value);
                if (settings.OffsetV.HasValue) d.SetDcOffsetVolts(settings.OffsetV.Value);
                if (settings.Cal) d.AmplitudeCalibration();
                return null;
            });
    }
}
