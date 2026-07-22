using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using GpibUtils.Common;
using GpibUtils.Instruments.SignalSources;
using GpibUtils.Verification;
using GpibUtils.Verification.Catalog;
using GpibUtils.Verification.References;
using GpibUtils.Visa;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>
    /// Interactive Spectre.Console verification harness. Walks the user through picking a device under test,
    /// then — for each quantity that verifies it — which reference instrument to measure with (offered as a
    /// menu whenever more than one instrument can do the job: e.g. an 8902A, an E4418B, a 438A/437B/436A for
    /// RF power). Then it runs the plan and prints a PASS/FAIL table. Fully drivable hardware-free against
    /// the <c>Simulated</c> provider. The one-shot <c>verify source</c> command exposes the same
    /// signal-source capability non-interactively for scripting (UI parity).
    /// </summary>
    public sealed class VerifyHarnessCommand : Command<VerifyHarnessCommand.Settings>
    {
        public sealed class Settings : ProviderSettings
        {
            [CommandOption("-t|--timeout <MS>")]
            [Description("I/O timeout in milliseconds (default 5000).")]
            public int TimeoutMs { get; set; } = 5000;
        }

        public override int Execute(CommandContext context, Settings settings) => Runner.Guard(() =>
        {
            AnsiConsole.Write(new Rule("[yellow]GPIBUtils verification harness[/]"));

            var provider = ResolveProviderInteractive(settings.Provider);
            AnsiConsole.MarkupLineInterpolated($"Provider: [green]{provider.Name}[/]");

            var category = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("What are you verifying?")
                .AddChoices("Signal generator / CW source", "DC voltage source (calibrator / power supply)"));

            return category.StartsWith("Signal", StringComparison.Ordinal)
                ? SourceHarness.RunInteractive(provider, settings.TimeoutMs)
                : SourceHarness.RunDcInteractive(provider, settings.TimeoutMs);
        });

        internal static IGpibProvider ResolveProviderInteractive(string provider)
        {
            if (!string.IsNullOrWhiteSpace(provider)) return GpibProviders.Get(provider);
            var names = GpibProviders.Names;
            if (names.Count <= 1) return GpibProviders.Default;
            var name = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("Select the GPIB [green]provider[/]:")
                .AddChoices(names));
            return GpibProviders.Get(name);
        }
    }

    /// <summary>
    /// One-shot signal-source verification (UI parity with the interactive harness): drive an
    /// <see cref="ISignalSource"/> through a plan of <c>freqMHz@powerDbm</c> points and read each back on a
    /// selectable power reference and/or frequency reference. Exit 1 on any FAIL.
    /// </summary>
    public sealed class VerifySourceCommand : Command<VerifySourceCommand.Settings>
    {
        public sealed class Settings : ProviderSettings
        {
            [CommandOption("--dut <KEY>")]
            [Description("Signal-source device key (e.g. hp8340b, hp8673b, e4438c).")]
            public string Dut { get; set; }

            [CommandOption("--points <LIST>")]
            [Description("Points as freqMHz@powerDbm, comma/space-separated, e.g. 100@0,500@0,1000@-10.")]
            public string Points { get; set; }

            [CommandOption("--power-ref <KEY>")]
            [Description("RF-power reference key (hp8902a, e4418b, hp438a, hp437b, hp436a). Omit to skip power.")]
            public string PowerRef { get; set; }

            [CommandOption("--freq-ref <KEY>")]
            [Description("Frequency reference key (hp53131a, hp5351a, hp5342a, hp5343a, hp8902a). Omit to skip frequency.")]
            public string FreqRef { get; set; }

            [CommandOption("--dut-addr <RESOURCE>")] [Description("DUT resource (default: configured / manual).")]
            public string DutAddr { get; set; }
            [CommandOption("--power-addr <RESOURCE>")] [Description("Power reference resource (default: configured / manual).")]
            public string PowerAddr { get; set; }
            [CommandOption("--freq-addr <RESOURCE>")] [Description("Frequency reference resource (default: configured / manual).")]
            public string FreqAddr { get; set; }

            [CommandOption("--power-tol-db <DB>")] [Description("Power tolerance in dB; enables PASS/FAIL on power.")]
            public double? PowerTolDb { get; set; }
            [CommandOption("--freq-tol-ppm <PPM>")] [Description("Frequency tolerance in ppm; enables PASS/FAIL on frequency.")]
            public double? FreqTolPpm { get; set; }

            [CommandOption("--samples <N>")] [Description("Reference reads averaged per point. Default 4.")]
            public int Samples { get; set; } = 4;
            [CommandOption("--settle <MS>")] [Description("Delay after setting each point, ms. Default 500.")]
            public int SettleMs { get; set; } = 500;
            [CommandOption("-t|--timeout <MS>")] [Description("I/O timeout in milliseconds (default 5000).")]
            public int TimeoutMs { get; set; } = 5000;
            [CommandOption("--csv <PATH>")] [Description("Write per-point results to this CSV file.")]
            public string Csv { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) => Runner.Guard(() =>
        {
            var dut = VerificationCatalog.SignalSourceDuts.FirstOrDefault(d => Eq(d.Key, settings.Dut));
            if (dut == null)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]Unknown or missing --dut.[/] Known: {string.Join(", ", VerificationCatalog.SignalSourceDuts.Select(d => d.Key))}");
                return 2;
            }
            if (string.IsNullOrWhiteSpace(settings.PowerRef) && string.IsNullOrWhiteSpace(settings.FreqRef))
            {
                AnsiConsole.MarkupLine("[red]Select at least one of --power-ref / --freq-ref.[/]");
                return 2;
            }

            ReferenceChoice powerChoice = null, freqChoice = null;
            if (!string.IsNullOrWhiteSpace(settings.PowerRef))
            {
                powerChoice = VerificationCatalog.RfPowerReferences.FirstOrDefault(r => Eq(r.Key, settings.PowerRef));
                if (powerChoice == null) { AnsiConsole.MarkupLine("[red]Unknown --power-ref.[/]"); return 2; }
            }
            if (!string.IsNullOrWhiteSpace(settings.FreqRef))
            {
                freqChoice = VerificationCatalog.FrequencyReferences.FirstOrDefault(r => Eq(r.Key, settings.FreqRef));
                if (freqChoice == null) { AnsiConsole.MarkupLine("[red]Unknown --freq-ref.[/]"); return 2; }
            }

            var plan = SourceHarness.ParseSourcePoints(settings.Points, settings.PowerTolDb, settings.FreqTolPpm);
            if (plan.Count == 0) { AnsiConsole.MarkupLine("[red]No points. Use --points freqMHz@powerDbm,…[/]"); return 2; }

            var provider = settings.Resolve();
            var options = new SignalSourceOptions
            {
                Samples = settings.Samples,
                SettlingMs = settings.SettleMs,
                DefaultPowerToleranceDb = settings.PowerTolDb,
                DefaultFrequencyTolerancePpm = settings.FreqTolPpm
            };
            return SourceHarness.RunSignalSource(provider, settings.TimeoutMs, dut, settings.DutAddr,
                powerChoice, settings.PowerAddr, freqChoice, settings.FreqAddr, plan, options, settings.Csv);
        });

        private static bool Eq(string a, string b) =>
            !string.IsNullOrWhiteSpace(b) && string.Equals(a, b.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
