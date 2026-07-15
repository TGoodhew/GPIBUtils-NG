using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GpibUtils.Instruments.Calibrators;
using GpibUtils.Instruments.Meters;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Verification.Tests
{
    /// <summary>Drives the <see cref="Fluke5440Verifier"/> against simulated 5440 + 34401A instruments.</summary>
    public class Fluke5440VerifierTests
    {
        private const string CalResource = "GPIB0::7::INSTR";
        private const string DmmResource = "GPIB0::22::INSTR";

        /// <summary>Builds a bench where the DMM reads the 5440's programmed output plus a fixed error (V),
        /// so a whole plan runs end to end deterministically.</summary>
        private static (Fluke5440Verifier verifier, Fluke5440ASimulatedDevice cal, Hp34401ASimulatedDevice dmm,
                        IDisposable sessions) Bench(double readbackErrorVolts = 0.0, VerificationOptions options = null)
        {
            var provider = new SimulatedGpibProvider();
            var cal = new Fluke5440ASimulatedDevice();
            var dmm = new Hp34401ASimulatedDevice();
            provider.Add(CalResource, cal.Instrument);
            provider.Add(DmmResource, dmm.Instrument);

            // Link the DMM's reads to the 5440's programmed output + a fixed error.
            dmm.Instrument.Responder = command =>
            {
                var u = (command ?? string.Empty).Trim().ToUpperInvariant();
                if (u == "READ?" || u == "FETCH?" || u == "FETC?")
                    return (cal.OutputVolts + readbackErrorVolts).ToString("G12", CultureInfo.InvariantCulture);
                return null;   // other queries fall back to the DMM's defaults
            };

            var calSession = provider.Open(CalResource);
            var dmmSession = provider.Open(DmmResource);
            var verifier = new Fluke5440Verifier(new Fluke5440A(calSession), new Hp34401A(dmmSession),
                                                 options ?? new VerificationOptions { SettlingMs = 0 });
            return (verifier, cal, dmm, new Disposer(calSession, dmmSession));
        }

        private sealed class Disposer : IDisposable
        {
            private readonly IDisposable[] _d;
            public Disposer(params IDisposable[] d) { _d = d; }
            public void Dispose() { foreach (var x in _d) x.Dispose(); }
        }

        private static VerificationPoint P(double v, double? tol = null, string range = null) =>
            new VerificationPoint { NominalVolts = v, TolerancePpm = tol, Range = range };

        [Fact]
        public void Ideal_chain_passes_within_tolerance()
        {
            var (verifier, _, _, sessions) = Bench(readbackErrorVolts: 0.0,
                options: new VerificationOptions { SettlingMs = 0, Samples = 4, DefaultTolerancePpm = 50 });
            using (sessions)
            {
                var results = verifier.Run(new[] { P(1), P(10), P(-10) });
                Assert.Equal(3, results.Count);
                Assert.All(results, r => Assert.Equal("PASS", r.Verdict));
                Assert.All(results, r => Assert.True(Math.Abs(r.PpmOfReading) < 1));
            }
        }

        [Fact]
        public void Out_of_tolerance_fails()
        {
            // +1 mV on a 10 V point = 100 ppm; a 50 ppm tolerance must FAIL it.
            var (verifier, _, _, sessions) = Bench(readbackErrorVolts: 0.001,
                options: new VerificationOptions { SettlingMs = 0, Samples = 1 });
            using (sessions)
            {
                var results = verifier.Run(new[] { P(10, tol: 50) });
                Assert.Equal("FAIL", results[0].Verdict);
                Assert.Equal(100, results[0].PpmOfReading, 0);
            }
        }

        [Fact]
        public void No_tolerance_is_report_only()
        {
            var (verifier, _, _, sessions) = Bench(options: new VerificationOptions { SettlingMs = 0 });
            using (sessions)
            {
                var results = verifier.Run(new[] { P(5) });
                Assert.Null(results[0].Verdict);
                Assert.False(results[0].Passed);
                Assert.False(results[0].Failed);
            }
        }

        [Fact]
        public void Programs_the_5440_and_reads_the_dmm()
        {
            var (verifier, cal, _, sessions) = Bench(options: new VerificationOptions { SettlingMs = 0 });
            using (sessions)
            {
                verifier.Run(new[] { P(7.5) });
                Assert.Equal(7.5, cal.OutputVolts, 5);
                Assert.Contains(cal.Commands, c => c.StartsWith("SOUT"));
                Assert.Contains("OPER", cal.Commands);
            }
        }

        [Fact]
        public void Standby_on_exit_returns_the_5440_to_standby()
        {
            var (verifier, cal, _, sessions) = Bench(options: new VerificationOptions { SettlingMs = 0, StandbyOnExit = true });
            using (sessions)
            {
                verifier.Run(new[] { P(1) });
                Assert.False(cal.IsOperating);
                Assert.Contains("STBY", cal.Commands);
            }
        }

        [Fact]
        public void Sense_mode_selects_esns_or_isns()
        {
            var (verifier, cal, _, sessions) = Bench(options: new VerificationOptions { SettlingMs = 0, SenseExternal = false });
            using (sessions)
            {
                verifier.Run(new[] { P(1) });
                Assert.Contains("ISNS", cal.Commands);
            }
        }

        [Fact]
        public void At_zero_uses_full_scale_for_the_verdict()
        {
            // 0 V nominal: ppm-of-reading is undefined, so the verdict uses |err|/full-scale.
            // 1 mV error on the 10 V range = 100 ppm of full-scale -> FAIL at 50 ppm.
            var (verifier, _, _, sessions) = Bench(readbackErrorVolts: 0.001,
                options: new VerificationOptions { SettlingMs = 0, GlobalRange = "10", DefaultTolerancePpm = 50 });
            using (sessions)
            {
                var results = verifier.Run(new[] { P(0) });
                Assert.Equal("FAIL", results[0].Verdict);
                Assert.True(double.IsNaN(results[0].PpmOfReading));
            }
        }

        // ---- plan parsing / CSV --------------------------------------------

        [Fact]
        public void Parse_inline_points()
        {
            var pts = VerificationPlan.ParseInlinePoints("0,1,-1,10 -10;100", globalRange: "AUTO", defaultTolerancePpm: 25);
            Assert.Equal(6, pts.Count);
            Assert.Equal(new[] { 0.0, 1, -1, 10, -10, 100 }, pts.Select(p => p.NominalVolts).ToArray());
            Assert.All(pts, p => Assert.Equal(25, p.TolerancePpm));
        }

        [Fact]
        public void Parse_plan_lines_with_header_and_columns()
        {
            var lines = new[]
            {
                "# a comment",
                "nominal_V,range,tolerance_ppm,notes",
                "10,10,20,ten volts",
                "-10,,,",
                "100,100,5,full scale"
            };
            var pts = VerificationPlan.ParsePlanLines(lines, globalRange: "AUTO", defaultTolerancePpm: 50);
            Assert.Equal(3, pts.Count);
            Assert.Equal(10, pts[0].NominalVolts);
            Assert.Equal("10", pts[0].Range);
            Assert.Equal(20, pts[0].TolerancePpm);
            Assert.Equal(50, pts[1].TolerancePpm);   // default applied where the column is blank
            Assert.Equal("full scale", pts[2].Notes);
        }

        [Fact]
        public void Parse_plan_requires_nominal_column()
        {
            Assert.Throws<FormatException>(() =>
                VerificationPlan.ParsePlanLines(new[] { "range,notes", "10,x" }, null, null));
        }

        [Fact]
        public void Csv_round_trips_the_schema()
        {
            var rows = new List<VerificationResult>
            {
                new VerificationResult { Index = 1, NominalVolts = 10, Range = "10", MeasuredVolts = 10.0001,
                    AbsErrorVolts = 0.0001, PpmOfReading = 10, StdDevVolts = 0, Samples = 4, TolerancePpm = 50, Verdict = "PASS" }
            };
            var csv = VerificationPlan.ToCsv(rows, "2026-07-15T00:00:00Z");
            Assert.Contains("idx,nominal_V,range", csv);
            Assert.Contains("PASS", csv);
            Assert.Contains("2026-07-15T00:00:00Z", csv);
        }
    }
}
