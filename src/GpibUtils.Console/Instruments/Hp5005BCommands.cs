using System;
using System.ComponentModel;
using GpibUtils.Instruments.Meters;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>Shared options for every <c>hp5005b</c> subcommand (the standard instrument options).</summary>
    public class Hp5005BSettings : InstrumentSettings
    {
        internal Hp5005B OpenDriver(out Visa.IInstrumentSession session)
        {
            session = OpenSession("hp5005b", Hp5005B.DefaultResource);
            return new Hp5005B(session);
        }
    }

    /// <summary>Shared execution shell: open, run, echo the commands sent, and (optionally) print a result.</summary>
    internal static class Hp5005BRunner
    {
        public static int Run(Hp5005BSettings settings, Func<Hp5005B, string> action) => Runner.Guard(() =>
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

    /// <summary>Query the instrument identity (ID).</summary>
    public sealed class Hp5005BIdnCommand : Command<Hp5005BIdnCommand.Settings>
    {
        public sealed class Settings : Hp5005BSettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp5005BRunner.Run(settings, d => d.Identify());
    }

    /// <summary>Device clear + reset to power-up defaults (clean known state).</summary>
    public sealed class Hp5005BInitCommand : Command<Hp5005BInitCommand.Settings>
    {
        public sealed class Settings : Hp5005BSettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp5005BRunner.Run(settings, d => { d.Initialize(); return null; });
    }

    /// <summary>Select a numeric function, trigger a measurement (QM/SRQ handshake), and read the value.</summary>
    public sealed class Hp5005BMeasureCommand : Command<Hp5005BMeasureCommand.Settings>
    {
        public sealed class Settings : Hp5005BSettings
        {
            [CommandArgument(0, "<FUNCTION>")]
            [Description("freq | totalize | interval | resistance | dcv | diff | ppeak | npeak")]
            public string Function { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp5005BRunner.Run(settings, d =>
            {
                var fn = Parse(settings.Function);
                double v = d.Measure(fn);
                return $"{fn}: {v}";
            });

        private static SignatureFunction Parse(string s)
        {
            switch ((s ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "freq": case "frequency": return SignatureFunction.Frequency;
                case "totalize": return SignatureFunction.Totalize;
                case "interval": case "ti": return SignatureFunction.TimeInterval;
                case "resistance": case "ohms": return SignatureFunction.Resistance;
                case "dcv": case "dc": return SignatureFunction.DcVoltage;
                case "diff": return SignatureFunction.DifferentialVoltage;
                case "ppeak": return SignatureFunction.PositivePeakVoltage;
                case "npeak": return SignatureFunction.NegativePeakVoltage;
                default: throw new ArgumentException($"Unknown function '{s}'.");
            }
        }
    }

    /// <summary>Capture a logic signature (NORM signature-analysis function) via the SRQ handshake.</summary>
    public sealed class Hp5005BSignatureCommand : Command<Hp5005BSignatureCommand.Settings>
    {
        public sealed class Settings : Hp5005BSettings
        {
            [CommandOption("--qual")]
            [Description("Use QUAL signature analysis (F1) instead of NORM (F0).")]
            public bool Qual { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp5005BRunner.Run(settings, d =>
            {
                d.SetFunction(settings.Qual ? SignatureFunction.QualSignature : SignatureFunction.NormSignature);
                return "signature: " + d.TriggerAndRead();
            });
    }

    /// <summary>Read the decimal error code (SE).</summary>
    public sealed class Hp5005BErrorCommand : Command<Hp5005BErrorCommand.Settings>
    {
        public sealed class Settings : Hp5005BSettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp5005BRunner.Run(settings, d => $"error code: {d.ReadErrorCode()}");
    }
}
