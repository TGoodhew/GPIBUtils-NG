using System;
using System.ComponentModel;
using System.Globalization;
using GpibUtils.Instruments.Meters;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>Shared options for every <c>dm3058</c> subcommand.</summary>
    public class RigolDm3058Settings : InstrumentSettings
    {
        internal RigolDm3058 OpenDriver(out Visa.IInstrumentSession session)
        {
            session = OpenSession("dm3058", RigolDm3058.DefaultResource);
            return new RigolDm3058(session);
        }
    }

    internal static class RigolDm3058Runner
    {
        public static int Run(RigolDm3058Settings settings, Func<RigolDm3058, string> action) => Runner.Guard(() =>
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

    public sealed class RigolDm3058IdnCommand : Command<RigolDm3058IdnCommand.Settings>
    {
        public sealed class Settings : RigolDm3058Settings { }
        public override int Execute(CommandContext context, Settings settings) =>
            RigolDm3058Runner.Run(settings, d => d.Identify());
    }

    public sealed class RigolDm3058InitCommand : Command<RigolDm3058InitCommand.Settings>
    {
        public sealed class Settings : RigolDm3058Settings { }
        public override int Execute(CommandContext context, Settings settings) =>
            RigolDm3058Runner.Run(settings, d => { d.Initialize(); return null; });
    }

    /// <summary>Take a one-shot measurement of a function.</summary>
    public sealed class RigolDm3058MeasureCommand : Command<RigolDm3058MeasureCommand.Settings>
    {
        public sealed class Settings : RigolDm3058Settings
        {
            [CommandArgument(0, "<function>")]
            [Description("Measurement function: dcv, acv, dci, aci, res, fres, freq, per, cont, diode.")]
            public string Function { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            RigolDm3058Runner.Run(settings, d =>
                d.Measure(DmmFunctionParser.Parse(settings.Function)).ToString("G7", CultureInfo.InvariantCulture));
    }
}
