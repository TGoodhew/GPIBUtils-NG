using System;
using System.ComponentModel;
using System.Linq;
using GpibUtils.Instruments.Analyzers;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>Shared options for every <c>hp8560e</c> subcommand (the standard instrument options).</summary>
    public class Hp8560ESettings : InstrumentSettings
    {
        /// <summary>Opens a session (default GPIB0::18::INSTR) and builds the driver.</summary>
        internal Hp8560E OpenDriver(out Visa.IInstrumentSession session)
        {
            session = OpenSession("hp8560e", Hp8560E.DefaultResource);
            return new Hp8560E(session);
        }
    }

    /// <summary>Shared execution shell: open, run, echo the commands sent, and (optionally) print a result.</summary>
    internal static class Hp8560ERunner
    {
        public static int Run(Hp8560ESettings settings, Func<Hp8560E, string> action) => Runner.Guard(() =>
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

    /// <summary>Query the instrument identity (ID?).</summary>
    public sealed class Hp8560EIdnCommand : Command<Hp8560EIdnCommand.Settings>
    {
        public sealed class Settings : Hp8560ESettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp8560ERunner.Run(settings, d => d.Identify());
    }

    /// <summary>Device clear + instrument preset + clear SRQ mask (clean known state).</summary>
    public sealed class Hp8560EInitCommand : Command<Hp8560EInitCommand.Settings>
    {
        public sealed class Settings : Hp8560ESettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp8560ERunner.Run(settings, d => { d.Initialize(); return null; });
    }

    /// <summary>Set center frequency + span, then take a single sweep with the SRQ handshake.</summary>
    public sealed class Hp8560ESweepCommand : Command<Hp8560ESweepCommand.Settings>
    {
        public sealed class Settings : Hp8560ESettings
        {
            [CommandOption("-c|--center <MHZ>")]
            [Description("Center frequency in MHz.")]
            public double? CenterMHz { get; set; }

            [CommandOption("-s|--span <MHZ>")]
            [Description("Frequency span in MHz.")]
            public double? SpanMHz { get; set; }

            [CommandOption("--rbw <HZ>")]
            [Description("Resolution bandwidth in Hz.")]
            public double? RbwHz { get; set; }

            [CommandOption("--peak")]
            [Description("After the sweep, place the marker on the peak and report it.")]
            public bool Peak { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp8560ERunner.Run(settings, d =>
            {
                if (settings.CenterMHz.HasValue) d.SetCenterFrequencyMHz(settings.CenterMHz.Value);
                if (settings.SpanMHz.HasValue) d.SetSpanHz(settings.SpanMHz.Value * 1e6);
                if (settings.RbwHz.HasValue) d.SetResolutionBandwidthHz(settings.RbwHz.Value);
                d.SingleSweep();
                if (settings.Peak)
                {
                    double amp = d.MarkerToPeakAmplitude();
                    double freq = d.MarkerFrequencyHz();
                    return $"sweep complete; peak {amp} dBm at {freq / 1e6:0.######} MHz";
                }
                return "sweep complete";
            });
    }

    /// <summary>Read the current trace (TRA?) and print a summary (min/max/points).</summary>
    public sealed class Hp8560ETraceCommand : Command<Hp8560ETraceCommand.Settings>
    {
        public sealed class Settings : Hp8560ESettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp8560ERunner.Run(settings, d =>
            {
                var trace = d.ReadTrace();
                return $"trace: {trace.Count} points, min {trace.Min()} / max {trace.Max()}";
            });
    }

    /// <summary>Place the marker on the peak and report its frequency + amplitude.</summary>
    public sealed class Hp8560EPeakCommand : Command<Hp8560EPeakCommand.Settings>
    {
        public sealed class Settings : Hp8560ESettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp8560ERunner.Run(settings, d =>
            {
                double amp = d.MarkerToPeakAmplitude();
                double freq = d.MarkerFrequencyHz();
                return $"peak {amp} dBm at {freq / 1e6:0.######} MHz";
            });
    }
}
