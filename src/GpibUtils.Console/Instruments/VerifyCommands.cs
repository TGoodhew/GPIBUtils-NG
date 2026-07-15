using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Threading;
using GpibUtils.Common;
using GpibUtils.Instruments.Calibrators;
using GpibUtils.Instruments.Meters;
using GpibUtils.Verification;
using GpibUtils.Visa;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>
    /// Non-interactive Fluke 5440 verification runner (issue #37): drive the 5440 through a list of voltage
    /// points and read each back through a 34401A, printing per-point ppm error + PASS/FAIL and (optionally)
    /// a CSV. Exit code 1 if any point fails its tolerance.
    /// </summary>
    public sealed class Verify5440Command : Command<Verify5440Command.Settings>
    {
        public sealed class Settings : ProviderSettings
        {
            [CommandOption("--points <LIST>")]
            [Description("Comma/space-separated nominal voltages, e.g. 0,1,-1,10,-10.")]
            public string Points { get; set; }

            [CommandOption("--plan-file <PATH>")]
            [Description("CSV plan with a nominal_V column (+ optional range, tolerance_ppm, notes).")]
            public string PlanFile { get; set; }

            [CommandOption("--range <V>")]
            [Description("DMM DCV range (V or AUTO). Default AUTO.")]
            public string Range { get; set; } = "AUTO";

            [CommandOption("--nplc <N>")] [Description("DMM integration time (NPLC). Default 10.")]
            public double Nplc { get; set; } = 10;

            [CommandOption("--sense <MODE>")] [Description("5440 sense: external (4-wire) or internal (2-wire). Default external.")]
            public string Sense { get; set; } = "external";

            [CommandOption("--samples <N>")] [Description("DMM reads averaged per point. Default 4.")]
            public int Samples { get; set; } = 4;

            [CommandOption("--settle <MS>")] [Description("Delay after OPER before sampling, ms. Default 1000.")]
            public int SettleMs { get; set; } = 1000;

            [CommandOption("--tolerance-ppm <N>")] [Description("Default per-point tolerance (ppm); enables PASS/FAIL.")]
            public double? TolerancePpm { get; set; }

            [CommandOption("--hi-z")] [Description("DMM INP:IMP:AUTO ON (>10 GΩ on the low DCV ranges).")]
            public bool HiZ { get; set; }

            [CommandOption("--csv <PATH>")] [Description("Write per-point results to this CSV file.")]
            public string Csv { get; set; }

            [CommandOption("-t|--timeout <MS>")] [Description("I/O timeout in milliseconds (default 5000).")]
            public int TimeoutMs { get; set; } = 5000;

            [CommandOption("--cal <RES>")] [Description("Fluke 5440 resource (default: configured / manual).")]
            public string Cal { get; set; }
            [CommandOption("--dmm <RES>")] [Description("34401A resource (default: configured / manual).")]
            public string Dmm { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) => Runner.Guard(() =>
        {
            List<VerificationPoint> plan;
            if (!string.IsNullOrWhiteSpace(settings.Points) && !string.IsNullOrWhiteSpace(settings.PlanFile))
            {
                AnsiConsole.MarkupLine("[red]Specify either --points or --plan-file, not both.[/]");
                return 2;
            }
            if (!string.IsNullOrWhiteSpace(settings.PlanFile))
                plan = VerificationPlan.ParsePlanFile(settings.PlanFile, settings.Range, settings.TolerancePpm);
            else if (!string.IsNullOrWhiteSpace(settings.Points))
                plan = VerificationPlan.ParseInlinePoints(settings.Points, settings.Range, settings.TolerancePpm);
            else
            {
                AnsiConsole.MarkupLine("[red]No plan. Use --points <list> or --plan-file <path>.[/]");
                return 2;
            }
            if (plan.Count == 0) { AnsiConsole.MarkupLine("[red]Plan is empty.[/]"); return 2; }

            var options = new VerificationOptions
            {
                GlobalRange = settings.Range,
                Nplc = settings.Nplc,
                HighImpedance = settings.HiZ,
                SenseExternal = !settings.Sense.Trim().StartsWith("int", StringComparison.OrdinalIgnoreCase),
                SettlingMs = settings.SettleMs,
                Samples = settings.Samples,
                DefaultTolerancePpm = settings.TolerancePpm
            };

            var provider = settings.Resolve();
            var store = InstrumentAddressStore.Load();
            IInstrumentSession Open(string addr, string key, string def) =>
                provider.Open(store.Resolve(addr, key, def), new SessionSettings { TimeoutMilliseconds = settings.TimeoutMs });

            using (var calSession = Open(settings.Cal, "fluke5440", Fluke5440A.DefaultResource))
            using (var dmmSession = Open(settings.Dmm, "hp34401a", Hp34401A.DefaultResource))
            {
                var verifier = new Fluke5440Verifier(new Fluke5440A(calSession), new Hp34401A(dmmSession), options)
                {
                    Log = msg => AnsiConsole.MarkupLineInterpolated($"[grey]{msg}[/]")
                };

                var results = verifier.Run(plan, Thread.Sleep);

                var table = new Table().Border(TableBorder.Rounded)
                    .AddColumn("#").AddColumn("Nominal V").AddColumn("Measured V").AddColumn("Err V")
                    .AddColumn("ppm").AddColumn("σ").AddColumn("Verdict");
                int pass = 0, fail = 0;
                foreach (var r in results)
                {
                    if (r.Passed) pass++; else if (r.Failed) fail++;
                    string verdict = r.Passed ? "[green]PASS[/]" : r.Failed ? "[red]FAIL[/]" : "[grey]-[/]";
                    table.AddRow(
                        r.Index.ToString(),
                        r.NominalVolts.ToString("G7", CultureInfo.InvariantCulture),
                        r.MeasuredVolts.ToString("G9", CultureInfo.InvariantCulture),
                        r.AbsErrorVolts.ToString("G4", CultureInfo.InvariantCulture),
                        double.IsNaN(r.PpmOfReading) ? "-" : r.PpmOfReading.ToString("F2", CultureInfo.InvariantCulture),
                        r.StdDevVolts.ToString("G3", CultureInfo.InvariantCulture),
                        verdict);
                }
                AnsiConsole.Write(table);
                AnsiConsole.MarkupLineInterpolated($"Points: {results.Count}   [green]PASS {pass}[/]   [red]FAIL {fail}[/]");

                if (!string.IsNullOrWhiteSpace(settings.Csv))
                {
                    File.WriteAllText(settings.Csv, VerificationPlan.ToCsv(results, DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)));
                    AnsiConsole.MarkupLineInterpolated($"[grey]wrote CSV: {settings.Csv}[/]");
                }

                return fail > 0 ? 1 : 0;
            }
        });
    }
}
