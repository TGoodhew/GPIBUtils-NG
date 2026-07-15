using System;
using System.ComponentModel;
using GpibUtils.Instruments.Calibrators;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>Shared options for every <c>fluke5440</c> subcommand (the standard instrument options).</summary>
    public class Fluke5440Settings : InstrumentSettings
    {
        /// <summary>Opens a session (default GPIB0::7::INSTR) and builds the driver.</summary>
        internal Fluke5440A OpenDriver(out Visa.IInstrumentSession session)
        {
            session = OpenSession("fluke5440", Fluke5440A.DefaultResource);
            return new Fluke5440A(session);
        }
    }

    /// <summary>Shared execution shell: open, run, echo the commands sent, and (optionally) print a result.</summary>
    internal static class Fluke5440Runner
    {
        public static int Run(Fluke5440Settings settings, Func<Fluke5440A, string> action) => Runner.Guard(() =>
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

    /// <summary>Show the fixed descriptor (5440 has no *IDN?).</summary>
    public sealed class Fluke5440IdnCommand : Command<Fluke5440IdnCommand.Settings>
    {
        public sealed class Settings : Fluke5440Settings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Fluke5440Runner.Run(settings, d => d.Identify());
    }

    /// <summary>Read the firmware version (GVRS) — the only identity over the bus.</summary>
    public sealed class Fluke5440FirmwareCommand : Command<Fluke5440FirmwareCommand.Settings>
    {
        public sealed class Settings : Fluke5440Settings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Fluke5440Runner.Run(settings, d => $"firmware (GVRS): {d.FirmwareVersion()}");
    }

    /// <summary>Device clear + RESET (power-on state: standby, output cleared).</summary>
    public sealed class Fluke5440InitCommand : Command<Fluke5440InitCommand.Settings>
    {
        public sealed class Settings : Fluke5440Settings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Fluke5440Runner.Run(settings, d => { d.Initialize(); return null; });
    }

    /// <summary>Reset to the power-on state (RESET).</summary>
    public sealed class Fluke5440ResetCommand : Command<Fluke5440ResetCommand.Settings>
    {
        public sealed class Settings : Fluke5440Settings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Fluke5440Runner.Run(settings, d => { d.Reset(); return null; });
    }

    /// <summary>Program the output voltage (SOUT), optionally switching to Operate.</summary>
    public sealed class Fluke5440SetCommand : Command<Fluke5440SetCommand.Settings>
    {
        public sealed class Settings : Fluke5440Settings
        {
            [CommandArgument(0, "<volts>")]
            [Description("Output level in volts (SOUT). The 5440 accepts < 8 significant digits.")]
            public double Volts { get; set; }

            [CommandOption("--operate")]
            [Description("Also switch the output to Operate (OPER) after setting the level.")]
            public bool Operate { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Fluke5440Runner.Run(settings, d =>
            {
                d.SetOutputVolts(settings.Volts);
                if (settings.Operate) d.Operate();
                return $"output set to {settings.Volts} V{(settings.Operate ? " (Operate)" : "")}";
            });
    }

    /// <summary>Read the present programmed output level (GOUT).</summary>
    public sealed class Fluke5440GetCommand : Command<Fluke5440GetCommand.Settings>
    {
        public sealed class Settings : Fluke5440Settings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Fluke5440Runner.Run(settings, d => $"output (GOUT): {d.GetOutputVolts()} V");
    }

    /// <summary>Switch the output to Operate (OPER).</summary>
    public sealed class Fluke5440OperateCommand : Command<Fluke5440OperateCommand.Settings>
    {
        public sealed class Settings : Fluke5440Settings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Fluke5440Runner.Run(settings, d => { d.Operate(); return "Operate"; });
    }

    /// <summary>Switch the output to Standby (STBY).</summary>
    public sealed class Fluke5440StandbyCommand : Command<Fluke5440StandbyCommand.Settings>
    {
        public sealed class Settings : Fluke5440Settings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Fluke5440Runner.Run(settings, d => { d.Standby(); return "Standby"; });
    }

    /// <summary>Select external 4-wire (ext) or internal 2-wire (int) sensing.</summary>
    public sealed class Fluke5440SenseCommand : Command<Fluke5440SenseCommand.Settings>
    {
        public sealed class Settings : Fluke5440Settings
        {
            [CommandArgument(0, "<mode>")]
            [Description("Sense mode: ext (4-wire, ESNS) or int (2-wire, ISNS).")]
            public string Mode { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Fluke5440Runner.Run(settings, d =>
            {
                var m = (settings.Mode ?? string.Empty).Trim().ToLowerInvariant();
                switch (m)
                {
                    case "ext": case "external": case "4": case "4wire":
                        d.SetSenseMode(CalibratorSenseMode.ExternalFourWire); return "external 4-wire sense";
                    case "int": case "internal": case "2": case "2wire":
                        d.SetSenseMode(CalibratorSenseMode.InternalTwoWire); return "internal 2-wire sense";
                    default:
                        throw new ArgumentException($"Unknown sense mode '{settings.Mode}'. Use ext or int.");
                }
            });
    }

    /// <summary>Read status, error, and doing-state (GSTS / GERR / GONG).</summary>
    public sealed class Fluke5440StatusCommand : Command<Fluke5440StatusCommand.Settings>
    {
        public sealed class Settings : Fluke5440Settings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Fluke5440Runner.Run(settings, d =>
            {
                var status = d.GetStatus();
                var error = d.GetError();
                var doing = d.GetDoingState();
                return $"status (GSTS): {status}   error (GERR): {error}   doing (GONG): {doing} ({(doing == 0 ? "idle" : "busy")})";
            });
    }
}
