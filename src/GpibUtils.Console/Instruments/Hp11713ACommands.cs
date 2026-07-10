using System;
using System.ComponentModel;
using System.Linq;
using GpibUtils.Instruments.Switches;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>
    /// Options common to every <c>hp11713a</c> subcommand: the shared instrument options plus the
    /// attenuator wiring and relay sense. Used as the branch settings, so these appear on each
    /// subcommand's <c>--help</c>.
    /// </summary>
    public class Hp11713ASettings : InstrumentSettings
    {
        [CommandOption("--config <NAME>")]
        [Description("Attenuator wiring: 'default' (8494 on X + 8496 on Y, 0-121 dB) or 'swapped'.")]
        public string ConfigName { get; set; } = "default";

        [CommandOption("--invert-sense")]
        [Description("Invert the A/B relay sense (for an attenuator cabled the opposite way).")]
        public bool InvertSense { get; set; }

        internal AttenuatorConfig ResolveConfig()
        {
            if (string.Equals(ConfigName, "default", StringComparison.OrdinalIgnoreCase))
                return AttenuatorConfig.Default();
            if (string.Equals(ConfigName, "swapped", StringComparison.OrdinalIgnoreCase))
                return AttenuatorConfig.Swapped();
            throw new ArgumentException($"Unknown --config '{ConfigName}' (expected 'default' or 'swapped').");
        }

        /// <summary>Opens a session (default GPIB0::28::INSTR) and builds a configured driver.</summary>
        internal Hp11713A OpenDriver(out Visa.IInstrumentSession session)
        {
            var config = ResolveConfig();
            session = OpenSession("hp11713a", Hp11713A.DefaultResource);
            return new Hp11713A(session, config) { InvertSense = InvertSense };
        }
    }

    /// <summary>Shared execution shell: open, run the driver action, print the resulting shadow state.</summary>
    internal static class Hp11713ARunner
    {
        public static int Run(Hp11713ASettings settings, Func<Hp11713A, string> action) => Runner.Guard(() =>
        {
            var driver = settings.OpenDriver(out var session);
            using (session)
            {
                var sent = action(driver);
                Report(driver, sent);
            }
            return 0;
        });

        private static void Report(Hp11713A d, string sent)
        {
            AnsiConsole.MarkupLineInterpolated($"[grey]sent   [/]: [green]{sent}[/]");
            AnsiConsole.MarkupLineInterpolated($"[grey]atten  [/]: [blue]{d.State.TotalDecibels(d.Config)} dB[/]");
            var engaged = d.State.Engaged.OrderBy(x => x).ToList();
            AnsiConsole.MarkupLineInterpolated(
                $"[grey]engaged[/]: {(engaged.Count == 0 ? "none" : string.Join(" ", engaged))}");
            if (d.State.Switch9.HasValue)
                AnsiConsole.MarkupLineInterpolated($"[grey]S9     [/]: {(d.State.Switch9.Value ? "A9 (on)" : "B9 (off)")}");
            if (d.State.Switch0.HasValue)
                AnsiConsole.MarkupLineInterpolated($"[grey]S0     [/]: {(d.State.Switch0.Value ? "A0 (on)" : "B0 (off)")}");
        }

        public static bool ParseOnOff(string value)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "on": case "1": case "true": case "a": case "yes": return true;
                case "off": case "0": case "false": case "b": case "no": return false;
                default: throw new ArgumentException($"Expected on/off (got '{value}').");
            }
        }
    }

    /// <summary>Sets total attenuation across both banks.</summary>
    public sealed class Hp11713ASetCommand : Command<Hp11713ASetCommand.Settings>
    {
        public sealed class Settings : Hp11713ASettings
        {
            [CommandArgument(0, "<db>")]
            [Description("Total attenuation in dB (0 to the configured maximum).")]
            public int Db { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp11713ARunner.Run(settings, d => d.SetAttenuationDb(settings.Db));
    }

    /// <summary>Engages exactly the given section digits (1-8) and bypasses the rest.</summary>
    public sealed class Hp11713AEngageCommand : Command<Hp11713AEngageCommand.Settings>
    {
        public sealed class Settings : Hp11713ASettings
        {
            [CommandArgument(0, "<digits>")]
            [Description("Section digits to engage, e.g. 1 3 6. Omit to bypass all (0 dB).")]
            public int[] Digits { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp11713ARunner.Run(settings, d => d.SetEngaged(settings.Digits ?? Array.Empty<int>()));
    }

    /// <summary>Sets all sections to 0 dB (device clear + all bypassed).</summary>
    public sealed class Hp11713AZeroCommand : Command<Hp11713AZeroCommand.Settings>
    {
        public sealed class Settings : Hp11713ASettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp11713ARunner.Run(settings, d => d.SetEngaged(Array.Empty<int>()));
    }

    /// <summary>Drives independent switch S9.</summary>
    public sealed class Hp11713ASwitch9Command : Command<Hp11713ASwitch9Command.Settings>
    {
        public sealed class Settings : Hp11713ASettings
        {
            [CommandArgument(0, "<state>")]
            [Description("on = A9, off = B9.")]
            public string State { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp11713ARunner.Run(settings, d => d.SetSwitch9(Hp11713ARunner.ParseOnOff(settings.State)));
    }

    /// <summary>Drives independent switch S0.</summary>
    public sealed class Hp11713ASwitch0Command : Command<Hp11713ASwitch0Command.Settings>
    {
        public sealed class Settings : Hp11713ASettings
        {
            [CommandArgument(0, "<state>")]
            [Description("on = A0, off = B0.")]
            public string State { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp11713ARunner.Run(settings, d => d.SetSwitch0(Hp11713ARunner.ParseOnOff(settings.State)));
    }

    /// <summary>Sends a raw 11713A data string verbatim (validated for legal characters).</summary>
    public sealed class Hp11713ARawCommand : Command<Hp11713ARawCommand.Settings>
    {
        public sealed class Settings : Hp11713ASettings
        {
            [CommandArgument(0, "<data>")]
            [Description("Raw data string, e.g. A13B24567890.")]
            public string Data { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) => Hp11713ARunner.Run(settings, d =>
        {
            if (!Hp11713ACommandBuilder.IsValidDataString(settings.Data))
                throw new ArgumentException($"'{settings.Data}' is not a valid 11713A data string (A/B + digits 0-9).");
            d.SendRaw(settings.Data);
            return settings.Data;
        });
    }
}
