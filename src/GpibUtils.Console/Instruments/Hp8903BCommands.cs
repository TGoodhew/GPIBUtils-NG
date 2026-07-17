using System;
using System.ComponentModel;
using GpibUtils.Instruments.Audio;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>Shared options for every <c>hp8903b</c> subcommand (the standard instrument options).</summary>
    public class Hp8903BSettings : InstrumentSettings
    {
        internal Hp8903B OpenDriver(out Visa.IInstrumentSession session)
        {
            session = OpenSession("hp8903b", Hp8903B.DefaultResource);
            return new Hp8903B(session);
        }
    }

    /// <summary>Shared execution shell: open, run, echo the commands sent, and (optionally) print a result.</summary>
    internal static class Hp8903BRunner
    {
        public static int Run(Hp8903BSettings settings, Func<Hp8903B, string> action) => Runner.Guard(() =>
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

    /// <summary>Show the instrument descriptor (8903B has no *IDN?).</summary>
    public sealed class Hp8903BIdnCommand : Command<Hp8903BIdnCommand.Settings>
    {
        public sealed class Settings : Hp8903BSettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp8903BRunner.Run(settings, d => d.Identify());
    }

    /// <summary>Device clear + Automatic Operation reset (clean known state).</summary>
    public sealed class Hp8903BInitCommand : Command<Hp8903BInitCommand.Settings>
    {
        public sealed class Settings : Hp8903BSettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp8903BRunner.Run(settings, d => { d.Initialize(); return null; });
    }

    /// <summary>Set the source frequency/amplitude, select a measurement, trigger and read (SRQ handshake).</summary>
    public sealed class Hp8903BMeasureCommand : Command<Hp8903BMeasureCommand.Settings>
    {
        public sealed class Settings : Hp8903BSettings
        {
            [CommandOption("-f|--frequency <HZ>")]
            [Description("Source frequency in Hz (default 1000).")]
            public double FrequencyHz { get; set; } = 1000;

            [CommandOption("-a|--amplitude <VOLTS>")]
            [Description("Source amplitude in volts (default 1).")]
            public double AmplitudeVolts { get; set; } = 1;

            [CommandOption("-m|--measurement <TYPE>")]
            [Description("aclevel | dclevel | distortion | distlevel | sinad | snr (default aclevel).")]
            public string Measurement { get; set; } = "aclevel";
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp8903BRunner.Run(settings, d =>
            {
                d.SetSourceFrequencyHz(settings.FrequencyHz);
                d.SetSourceAmplitude(settings.AmplitudeVolts, AudioAmplitudeUnit.Volts);
                d.SetMeasurement(Parse(settings.Measurement));
                double v = d.Measure();
                return $"{Parse(settings.Measurement)}: {v:G6}";
            });

        private static AudioMeasurement Parse(string s)
        {
            switch ((s ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "aclevel": case "ac": return AudioMeasurement.AcLevel;
                case "dclevel": case "dc": return AudioMeasurement.DcLevel;
                case "distortion": case "thd": return AudioMeasurement.Distortion;
                case "distlevel": return AudioMeasurement.DistortionLevel;
                case "sinad": return AudioMeasurement.Sinad;
                case "snr": return AudioMeasurement.SignalToNoise;
                default: throw new ArgumentException($"Unknown measurement '{s}'.");
            }
        }
    }
}
