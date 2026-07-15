using System;
using System.ComponentModel;
using System.Linq;
using GpibUtils.Instruments.Analyzers;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>Shared options for every <c>e4406a</c> subcommand (the standard instrument options).</summary>
    public class AgilentE4406ASettings : InstrumentSettings
    {
        /// <summary>Opens a session (default GPIB0::18::INSTR) and builds the driver.</summary>
        internal AgilentE4406A OpenDriver(out Visa.IInstrumentSession session)
        {
            session = OpenSession("e4406a", AgilentE4406A.DefaultResource);
            return new AgilentE4406A(session);
        }
    }

    /// <summary>Shared execution shell: open, run, echo the commands sent, and (optionally) print a result.</summary>
    internal static class AgilentE4406ARunner
    {
        public static int Run(AgilentE4406ASettings settings, Func<AgilentE4406A, string> action) => Runner.Guard(() =>
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

    /// <summary>Query the instrument identity (*IDN?).</summary>
    public sealed class AgilentE4406AIdnCommand : Command<AgilentE4406AIdnCommand.Settings>
    {
        public sealed class Settings : AgilentE4406ASettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            AgilentE4406ARunner.Run(settings, d => d.Identify());
    }

    /// <summary>Device clear + *RST + *CLS + Basic single mode (clean known state).</summary>
    public sealed class AgilentE4406AInitCommand : Command<AgilentE4406AInitCommand.Settings>
    {
        public sealed class Settings : AgilentE4406ASettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            AgilentE4406ARunner.Run(settings, d => { d.Initialize(); return null; });
    }

    /// <summary>Measure Channel Power at a center frequency (MHz).</summary>
    public sealed class AgilentE4406AChPowerCommand : Command<AgilentE4406AChPowerCommand.Settings>
    {
        public sealed class Settings : AgilentE4406ASettings
        {
            [CommandArgument(0, "<center_mhz>")]
            [Description("Channel center frequency in MHz.")]
            public double CenterMHz { get; set; }

            [CommandOption("-s|--span <MHZ>")]
            [Description("Per-measurement span in MHz (0 leaves the current span).")]
            public double SpanMHz { get; set; }

            [CommandOption("-b|--bandwidth <MHZ>")]
            [Description("Integration bandwidth in MHz (0 leaves the current setting).")]
            public double BandwidthMHz { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            AgilentE4406ARunner.Run(settings, d =>
            {
                var r = d.MeasureChannelPower(settings.CenterMHz * 1e6, settings.SpanMHz * 1e6, settings.BandwidthMHz * 1e6);
                return $"channel power: {r.TotalPowerDbm} dBm, PSD {r.PowerSpectralDensityDbmHz} dBm/Hz";
            });
    }

    /// <summary>Measure Adjacent Channel Power at a center frequency (MHz).</summary>
    public sealed class AgilentE4406AAcpCommand : Command<AgilentE4406AAcpCommand.Settings>
    {
        public sealed class Settings : AgilentE4406ASettings
        {
            [CommandArgument(0, "<center_mhz>")]
            [Description("Carrier center frequency in MHz.")]
            public double CenterMHz { get; set; }

            [CommandOption("-b|--bandwidth <MHZ>")]
            [Description("Carrier integration bandwidth in MHz (0 leaves the current setting).")]
            public double BandwidthMHz { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            AgilentE4406ARunner.Run(settings, d =>
            {
                var s = d.MeasureAcp(settings.CenterMHz * 1e6, settings.BandwidthMHz * 1e6);
                return $"ACP scalars: {string.Join(", ", s)}";
            });
    }

    /// <summary>Run a raw measurement by root (READ) and print the scalar set.</summary>
    public sealed class AgilentE4406AMeasureCommand : Command<AgilentE4406AMeasureCommand.Settings>
    {
        public sealed class Settings : AgilentE4406ASettings
        {
            [CommandArgument(0, "<root>")]
            [Description("Measurement root: CHPower, ACP, PSTatistic (CCDF), WAVeform, SPECtrum.")]
            public string Root { get; set; }

            [CommandOption("-c|--center <MHZ>")]
            [Description("Center frequency in MHz to set before reading (optional).")]
            public double? CenterMHz { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            AgilentE4406ARunner.Run(settings, d =>
            {
                d.SelectBasicMode();
                d.SetSingleMeasurement();
                if (settings.CenterMHz.HasValue) d.SetCenterFrequencyMHz(settings.CenterMHz.Value);
                var s = d.Read(settings.Root);
                return $"{settings.Root}: {string.Join(", ", s)}";
            });
    }

    /// <summary>Read the head of the SCPI error queue (:SYSTem:ERRor?).</summary>
    public sealed class AgilentE4406AErrorCommand : Command<AgilentE4406AErrorCommand.Settings>
    {
        public sealed class Settings : AgilentE4406ASettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            AgilentE4406ARunner.Run(settings, d => $"error: {d.GetError()}");
    }
}
