using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using GpibUtils.Common;
using GpibUtils.Instruments.SignalSources;
using GpibUtils.Verification;
using GpibUtils.Verification.Catalog;
using GpibUtils.Verification.References;
using GpibUtils.Visa;
using Spectre.Console;

namespace GpibUtils.Console.Instruments
{
    /// <summary>
    /// Shared implementation behind the interactive <c>verify harness</c> and the one-shot
    /// <c>verify source</c> command: reference selection, session wiring, plan parsing, the run itself, and
    /// result rendering. Kept UI-thin so both front-ends produce identical behaviour (UI parity).
    /// </summary>
    internal static class SourceHarness
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ---- interactive: signal source ---------------------------------------------------------------

        public static int RunInteractive(IGpibProvider provider, int timeoutMs)
        {
            var dut = AnsiConsole.Prompt(new SelectionPrompt<InstrumentChoice<ISignalSource>>()
                .Title("Select the [green]signal source[/] to verify:")
                .PageSize(15)
                .UseConverter(c => c.Description)
                .AddChoices(VerificationCatalog.SignalSourceDuts));

            bool doPower = AnsiConsole.Confirm("Verify RF [green]power[/]?");
            bool doFreq = AnsiConsole.Confirm("Verify [green]frequency[/]?");
            if (!doPower && !doFreq) { AnsiConsole.MarkupLine("[red]Nothing selected to verify.[/]"); return 2; }

            var powerChoice = doPower ? PickReference("RF power (dBm)", VerificationCatalog.RfPowerReferences) : null;
            var freqChoice = doFreq ? PickReference("frequency (Hz)", VerificationCatalog.FrequencyReferences) : null;

            var store = InstrumentAddressStore.Load();
            string dutAddr = AskAddress(dut.Description, dut.Key, dut.DefaultResource, store);
            string powerAddr = powerChoice != null ? AskAddress(powerChoice.Description, powerChoice.Key, powerChoice.DefaultResource, store) : null;
            string freqAddr = freqChoice != null ? AskAddress(freqChoice.Description, freqChoice.Key, freqChoice.DefaultResource, store) : null;

            string rawPoints = AnsiConsole.Prompt(new TextPrompt<string>("Points ([grey]freqMHz@powerDbm, …[/]):")
                .DefaultValue("100@0, 500@0, 1000@-10"));
            var plan = ParseSourcePoints(rawPoints, null, null);
            if (plan.Count == 0) { AnsiConsole.MarkupLine("[red]No valid points.[/]"); return 2; }

            var options = new SignalSourceOptions
            {
                Samples = AnsiConsole.Prompt(new TextPrompt<int>("Samples per point:").DefaultValue(4)),
                SettlingMs = AnsiConsole.Prompt(new TextPrompt<int>("Settle ms:").DefaultValue(500)),
            };
            if (doPower) options.DefaultPowerToleranceDb = AskOptionalDouble("Power tolerance dB ([grey]blank = report-only[/]):");
            if (doFreq) options.DefaultFrequencyTolerancePpm = AskOptionalDouble("Frequency tolerance ppm ([grey]blank = report-only[/]):");

            return RunSignalSource(provider, timeoutMs, dut, dutAddr, powerChoice, powerAddr, freqChoice, freqAddr, plan, options, null);
        }

        // ---- interactive: DC source -------------------------------------------------------------------

        public static int RunDcInteractive(IGpibProvider provider, int timeoutMs)
        {
            var dut = AnsiConsole.Prompt(new SelectionPrompt<InstrumentChoice<IVoltageSourceDut>>()
                .Title("Select the [green]DC source[/] to verify:")
                .UseConverter(c => c.Description)
                .AddChoices(VerificationCatalog.DcSourceDuts));
            var refChoice = PickReference("DC voltage (V)", VerificationCatalog.DcVoltageReferences);

            var store = InstrumentAddressStore.Load();
            string dutAddr = AskAddress(dut.Description, dut.Key, dut.DefaultResource, store);
            string refAddr = AskAddress(refChoice.Description, refChoice.Key, refChoice.DefaultResource, store);

            string rawPoints = AnsiConsole.Prompt(new TextPrompt<string>("Voltage points ([grey]e.g. 0,1,-1,10,-10[/]):")
                .DefaultValue("0,1,-1,10,-10"));
            var tol = AskOptionalDouble("Tolerance ppm ([grey]blank = report-only[/]):");
            var plan = VerificationPlan.ParseInlinePoints(rawPoints, "AUTO", tol);
            if (plan.Count == 0) { AnsiConsole.MarkupLine("[red]No valid points.[/]"); return 2; }

            var options = new DcSourceOptions
            {
                Samples = AnsiConsole.Prompt(new TextPrompt<int>("Samples per point:").DefaultValue(4)),
                SettlingMs = AnsiConsole.Prompt(new TextPrompt<int>("Settle ms:").DefaultValue(1000)),
                DefaultTolerancePpm = tol
            };

            var ss = new SessionSettings { TimeoutMilliseconds = timeoutMs };
            string dutResource = store.Resolve(dutAddr, dut.Key, dut.DefaultResource);
            string refResource = store.Resolve(refAddr, refChoice.Key, refChoice.DefaultResource);

            // Seed before opening — see the note in RunSignalSource.
            var coupling = SimulatedHarnessBench.TrySeed(provider,
                new[] { new SimReferenceRequest(refResource, refChoice) });

            var dutSession = provider.Open(dutResource, ss);
            IReferenceMeasurement voltRef = null;
            try
            {
                var source = dut.Open(dutSession);
                if (coupling != null) source = coupling.Couple(source);
                voltRef = refChoice.Open(provider.Open(refResource, ss));
                var verifier = new DcSourceVerifier(source, voltRef, options);
                AnsiConsole.MarkupLineInterpolated($"DUT: [green]{dut.Description}[/]   Reference: [green]{voltRef.DisplayName}[/]");
                WriteSimulatedBenchBanner(coupling);

                IReadOnlyList<VerificationResult> results = null;
                AnsiConsole.Status().Start("Running verification…", _ =>
                {
                    results = verifier.Run(plan, Thread.Sleep);
                });
                return RenderDcResults(results) > 0 ? 1 : 0;
            }
            finally
            {
                voltRef?.Dispose();
                dutSession?.Dispose();
            }
        }

        // ---- core signal-source runner (shared by interactive + one-shot) -----------------------------

        public static int RunSignalSource(IGpibProvider provider, int timeoutMs,
            InstrumentChoice<ISignalSource> dut, string dutAddr,
            ReferenceChoice powerChoice, string powerAddr,
            ReferenceChoice freqChoice, string freqAddr,
            IReadOnlyList<SignalSourcePoint> plan, SignalSourceOptions options, string csvPath)
        {
            var store = InstrumentAddressStore.Load();
            var ss = new SessionSettings { TimeoutMilliseconds = timeoutMs };

            string dutResource = store.Resolve(dutAddr, dut.Key, dut.DefaultResource);
            string powerResource = powerChoice != null ? store.Resolve(powerAddr, powerChoice.Key, powerChoice.DefaultResource) : null;
            string freqResource = freqChoice != null ? store.Resolve(freqAddr, freqChoice.Key, freqChoice.DefaultResource) : null;

            // Under the Simulated provider, seed the reference models BEFORE opening anything — the
            // provider auto-creates a generic instrument at first open, and a generic one never raises the
            // status bits the reference drivers' completion handshakes wait on.
            var seedRequests = new List<SimReferenceRequest>();
            if (powerChoice != null) seedRequests.Add(new SimReferenceRequest(powerResource, powerChoice));
            if (freqChoice != null) seedRequests.Add(new SimReferenceRequest(freqResource, freqChoice));
            var coupling = SimulatedHarnessBench.TrySeed(provider, seedRequests);

            var dutSession = provider.Open(dutResource, ss);
            IReferenceMeasurement powerRef = null, freqRef = null;
            try
            {
                var source = dut.Open(dutSession);
                if (coupling != null) source = coupling.Couple(source);
                if (powerChoice != null) powerRef = powerChoice.Open(provider.Open(powerResource, ss));
                if (freqChoice != null) freqRef = freqChoice.Open(provider.Open(freqResource, ss));

                var verifier = new SignalSourceVerifier(source, powerRef, freqRef, options);

                AnsiConsole.MarkupLineInterpolated($"DUT: [green]{dut.Description}[/] @ {dutResource}");
                if (powerRef != null) AnsiConsole.MarkupLineInterpolated($"Power reference:     [green]{powerRef.DisplayName}[/]");
                if (freqRef != null) AnsiConsole.MarkupLineInterpolated($"Frequency reference: [green]{freqRef.DisplayName}[/]");
                WriteSimulatedBenchBanner(coupling);

                IReadOnlyList<SignalSourceResult> results = null;
                AnsiConsole.Status().Start("Running verification…", _ =>
                {
                    results = verifier.Run(plan, Thread.Sleep);
                });

                int fail = RenderSourceResults(results, powerRef != null, freqRef != null);
                if (!string.IsNullOrWhiteSpace(csvPath))
                {
                    File.WriteAllText(csvPath, SourceResultsCsv(results));
                    AnsiConsole.MarkupLineInterpolated($"[grey]wrote CSV: {csvPath}[/]");
                }
                return fail > 0 ? 1 : 0;
            }
            finally
            {
                powerRef?.Dispose();
                freqRef?.Dispose();
                dutSession?.Dispose();
            }
        }

        // ---- helpers ----------------------------------------------------------------------------------

        /// <summary>
        /// States plainly what a simulated run is worth. The coupling feeds each reference the DUT's
        /// commanded set-point exactly, so every graded point passes regardless of tolerance — useful for
        /// exercising the harness, useless for judging an instrument. Also surfaces any references that had
        /// no simulated model and so fell through to a generic instrument.
        /// </summary>
        private static void WriteSimulatedBenchBanner(SimulatedBenchCoupling coupling)
        {
            if (coupling == null) return;
            if (coupling.AnySeeded)
                AnsiConsole.MarkupLine(
                    "[yellow]Simulated bench[/] — the references are seeded with their device models and read back the " +
                    "DUT's commanded set-point [yellow]exactly[/], so every graded point PASSes by construction. " +
                    "Use this to exercise the harness, not to judge an instrument.");
            else
                AnsiConsole.MarkupLine(
                    "[red]Simulated bench unavailable[/] — none of the selected references has a simulated model, so " +
                    "they fall through to a generic instrument that never answers. This run will fail; pick a " +
                    "reference listed below as supported, or use real hardware.");
            foreach (var warning in coupling.Warnings)
                AnsiConsole.MarkupLineInterpolated($"[yellow]  warning:[/] {warning}");
        }

        public static List<SignalSourcePoint> ParseSourcePoints(string raw, double? tolDb, double? tolPpm)
        {
            var points = new List<SignalSourcePoint>();
            if (string.IsNullOrWhiteSpace(raw)) return points;
            foreach (var token in raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var t = token.Trim();
                if (t.Length == 0) continue;
                var parts = t.Split('@');
                if (!double.TryParse(parts[0].Trim(), NumberStyles.Float, Inv, out var freq))
                    throw new FormatException($"Could not parse frequency in point '{t}'.");
                double power = 0;
                if (parts.Length > 1 && parts[1].Trim().Length > 0 &&
                    !double.TryParse(parts[1].Trim(), NumberStyles.Float, Inv, out power))
                    throw new FormatException($"Could not parse power in point '{t}'.");
                points.Add(new SignalSourcePoint
                {
                    FrequencyMHz = freq,
                    PowerDbm = power,
                    PowerToleranceDb = tolDb,
                    FrequencyTolerancePpm = tolPpm
                });
            }
            return points;
        }

        private static ReferenceChoice PickReference(string roleTitle, IReadOnlyList<ReferenceChoice> candidates)
        {
            if (candidates.Count == 1)
            {
                AnsiConsole.MarkupLineInterpolated($"[grey]Only one {roleTitle} reference available:[/] {candidates[0].Description}");
                return candidates[0];
            }
            return AnsiConsole.Prompt(new SelectionPrompt<ReferenceChoice>()
                .Title($"Select the [green]{roleTitle}[/] reference:")
                .UseConverter(c => c.Description)
                .AddChoices(candidates));
        }

        private static string AskAddress(string label, string key, string def, InstrumentAddressStore store)
        {
            var current = store.Resolve(null, key, def);
            return AnsiConsole.Prompt(new TextPrompt<string>($"[blue]{Markup.Escape(label)}[/] resource:").DefaultValue(current));
        }

        private static double? AskOptionalDouble(string title)
        {
            var s = AnsiConsole.Prompt(new TextPrompt<string>(title).AllowEmpty());
            if (string.IsNullOrWhiteSpace(s)) return null;
            return double.Parse(s.Trim(), NumberStyles.Float, Inv);
        }

        private static int RenderSourceResults(IReadOnlyList<SignalSourceResult> results, bool power, bool freq)
        {
            var table = new Table().Border(TableBorder.Rounded)
                .AddColumn("#").AddColumn("Freq MHz").AddColumn("Set dBm");
            if (power) table.AddColumn("Meas dBm").AddColumn("Δ dB").AddColumn("P");
            if (freq) table.AddColumn("Meas Hz").AddColumn("Δ ppm").AddColumn("F");

            int fail = 0, err = 0;
            foreach (var r in results)
            {
                var row = new List<string>
                {
                    r.Index.ToString(CultureInfo.InvariantCulture),
                    r.FrequencyMHz.ToString("G9", Inv),
                    r.PowerDbm.ToString("G4", Inv)
                };
                if (power)
                {
                    row.Add(r.PowerMeasured ? r.MeasuredPowerDbm.ToString("F3", Inv) : "-");
                    row.Add(r.PowerMeasured ? r.PowerErrorDb.ToString("F3", Inv) : "-");
                    row.Add(Verdict(r.PowerVerdict, r.Errored));
                }
                if (freq)
                {
                    row.Add(r.FrequencyMeasured ? r.MeasuredFrequencyHz.ToString("G10", Inv) : "-");
                    row.Add(r.FrequencyMeasured && !double.IsNaN(r.FrequencyErrorPpm) ? r.FrequencyErrorPpm.ToString("F3", Inv) : "-");
                    row.Add(Verdict(r.FrequencyVerdict, r.Errored));
                }
                if (r.Errored) err++; else if (r.Failed) fail++;
                table.AddRow(row.ToArray());
            }
            AnsiConsole.Write(table);
            int pass = results.Count(x => x.Passed);
            foreach (var r in results.Where(x => x.Errored))
                AnsiConsole.MarkupLineInterpolated($"[yellow]point {r.Index} error:[/] {r.Error}");
            AnsiConsole.MarkupLineInterpolated($"Points: {results.Count}   [green]PASS {pass}[/]   [red]FAIL {fail}[/]   [yellow]ERROR {err}[/]");
            return fail + err;
        }

        private static int RenderDcResults(IReadOnlyList<VerificationResult> results)
        {
            var table = new Table().Border(TableBorder.Rounded)
                .AddColumn("#").AddColumn("Nominal V").AddColumn("Measured V").AddColumn("Err V")
                .AddColumn("ppm").AddColumn("σ").AddColumn("Verdict");
            int fail = 0, pass = 0, err = 0;
            foreach (var r in results)
            {
                if (r.Errored) err++; else if (r.Passed) pass++; else if (r.Failed) fail++;
                table.AddRow(
                    r.Index.ToString(CultureInfo.InvariantCulture),
                    r.NominalVolts.ToString("G7", Inv),
                    r.Errored ? "-" : r.MeasuredVolts.ToString("G9", Inv),
                    r.Errored ? "-" : r.AbsErrorVolts.ToString("G4", Inv),
                    double.IsNaN(r.PpmOfReading) ? "-" : r.PpmOfReading.ToString("F2", Inv),
                    r.Errored ? "-" : r.StdDevVolts.ToString("G3", Inv),
                    Verdict(r.Verdict));
            }
            AnsiConsole.Write(table);
            foreach (var r in results.Where(x => x.Errored))
                AnsiConsole.MarkupLineInterpolated($"[yellow]point {r.Index} error:[/] {r.Error}");
            AnsiConsole.MarkupLineInterpolated($"Points: {results.Count}   [green]PASS {pass}[/]   [red]FAIL {fail}[/]   [yellow]ERROR {err}[/]");
            return fail + err;
        }

        private static string Verdict(string v, bool errored = false) =>
            errored || v == "ERROR" ? "[yellow]ERROR[/]" :
            v == "PASS" ? "[green]PASS[/]" : v == "FAIL" ? "[red]FAIL[/]" : "[grey]-[/]";

        private static string SourceResultsCsv(IReadOnlyList<SignalSourceResult> results)
        {
            var sb = new StringBuilder();
            sb.AppendLine("idx,freq_MHz,set_dBm,meas_dBm,err_dB,power_tol_dB,power_verdict,meas_Hz,err_ppm,freq_tol_ppm,freq_verdict,notes");
            foreach (var r in results)
            {
                sb.Append(r.Index).Append(',')
                  .Append(r.FrequencyMHz.ToString("G9", Inv)).Append(',')
                  .Append(r.PowerDbm.ToString("G6", Inv)).Append(',')
                  .Append(r.PowerMeasured ? r.MeasuredPowerDbm.ToString("G6", Inv) : "").Append(',')
                  .Append(r.PowerMeasured ? r.PowerErrorDb.ToString("G6", Inv) : "").Append(',')
                  .Append(r.PowerToleranceDb?.ToString("G6", Inv) ?? "").Append(',')
                  .Append(r.PowerVerdict ?? "").Append(',')
                  .Append(r.FrequencyMeasured ? r.MeasuredFrequencyHz.ToString("G10", Inv) : "").Append(',')
                  .Append(r.FrequencyMeasured && !double.IsNaN(r.FrequencyErrorPpm) ? r.FrequencyErrorPpm.ToString("G6", Inv) : "").Append(',')
                  .Append(r.FrequencyTolerancePpm?.ToString("G6", Inv) ?? "").Append(',')
                  .Append(r.Errored ? "ERROR" : (r.FrequencyVerdict ?? "")).Append(',')
                  .Append(((r.Errored ? "ERROR: " + r.Error + " " : "") + (r.Notes ?? "")).Replace(",", " "));
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
