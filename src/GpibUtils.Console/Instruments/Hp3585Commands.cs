using System;
using System.ComponentModel;
using System.Linq;
using GpibUtils.Instruments.Analyzers;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>Shared options for every <c>hp3585</c> subcommand (the standard instrument options).</summary>
    public class Hp3585Settings : InstrumentSettings
    {
        /// <summary>Opens a session (default GPIB0::11::INSTR) and builds the driver.</summary>
        internal Hp3585 OpenDriver(out Visa.IInstrumentSession session)
        {
            session = OpenSession("hp3585", Hp3585.DefaultResource);
            return new Hp3585(session);
        }
    }

    /// <summary>Shared execution shell: open, run, echo the commands sent, and (optionally) print a result.</summary>
    internal static class Hp3585Runner
    {
        public static int Run(Hp3585Settings settings, Func<Hp3585, string> action) => Runner.Guard(() =>
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

    /// <summary>Show the instrument descriptor (3585 has no *IDN?).</summary>
    public sealed class Hp3585IdnCommand : Command<Hp3585IdnCommand.Settings>
    {
        public sealed class Settings : Hp3585Settings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp3585Runner.Run(settings, d => d.Identify());
    }

    /// <summary>Device clear + preset + disable operation-complete SRQ (clean known state).</summary>
    public sealed class Hp3585InitCommand : Command<Hp3585InitCommand.Settings>
    {
        public sealed class Settings : Hp3585Settings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp3585Runner.Run(settings, d => { d.Initialize(); return null; });
    }

    /// <summary>Set center frequency + span, then take a single sweep with the CQ/serial-poll handshake.</summary>
    public sealed class Hp3585SweepCommand : Command<Hp3585SweepCommand.Settings>
    {
        public sealed class Settings : Hp3585Settings
        {
            [CommandOption("-c|--center <MHZ>")]
            [Description("Center frequency in MHz.")]
            public double? CenterMHz { get; set; }

            [CommandOption("-s|--span <KHZ>")]
            [Description("Frequency span in kHz.")]
            public double? SpanKHz { get; set; }

            [CommandOption("--rbw <HZ>")]
            [Description("Resolution bandwidth in Hz.")]
            public double? RbwHz { get; set; }

            [CommandOption("--peak")]
            [Description("After the sweep, report the peak amplitude (found from the trace).")]
            public bool Peak { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp3585Runner.Run(settings, d =>
            {
                if (settings.CenterMHz.HasValue) d.SetCenterFrequencyMHz(settings.CenterMHz.Value);
                if (settings.SpanKHz.HasValue) d.SetSpanHz(settings.SpanKHz.Value * 1e3);
                if (settings.RbwHz.HasValue) d.SetResolutionBandwidthHz(settings.RbwHz.Value);
                d.SingleSweep();
                return settings.Peak ? $"sweep complete; peak {d.MarkerToPeakAmplitude()} dB" : "sweep complete";
            });
    }

    /// <summary>Read the current trace (D3 dump) and print a summary (min/max/points).</summary>
    public sealed class Hp3585TraceCommand : Command<Hp3585TraceCommand.Settings>
    {
        public sealed class Settings : Hp3585Settings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp3585Runner.Run(settings, d =>
            {
                var trace = d.ReadTrace();
                return $"trace: {trace.Count} points, min {trace.Min()} / max {trace.Max()}";
            });
    }

    /// <summary>Read the marker frequency + amplitude (D2 / D1 dumps).</summary>
    public sealed class Hp3585MarkerCommand : Command<Hp3585MarkerCommand.Settings>
    {
        public sealed class Settings : Hp3585Settings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp3585Runner.Run(settings, d =>
            {
                double freq = d.MarkerFrequencyHz();
                double amp = d.MarkerAmplitude();
                return $"marker {amp} dB at {freq / 1e6:0.######} MHz";
            });
    }
}
