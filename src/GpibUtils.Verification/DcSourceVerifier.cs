using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Instruments.Meters;
using GpibUtils.Verification.References;

namespace GpibUtils.Verification
{
    /// <summary>Run-wide options for a <see cref="DcSourceVerifier"/> run.</summary>
    public sealed class DcSourceOptions
    {
        /// <summary>Delay after enabling the output before sampling, ms. Default 1000.</summary>
        public int SettlingMs { get; set; } = 1000;

        /// <summary>Reference reads averaged per point. Default 4.</summary>
        public int Samples { get; set; } = 4;

        /// <summary>Default per-point tolerance in ppm; null = report-only unless a point sets its own.</summary>
        public double? DefaultTolerancePpm { get; set; }

        /// <summary>Full-scale (V) used for the ppm verdict at a 0 V nominal (where ppm-of-reading is
        /// undefined); the point's own numeric range overrides it. Default 10.</summary>
        public double ZeroFullScaleVolts { get; set; } = 10.0;

        /// <summary>Disable the DUT's output when the run finishes. Default true.</summary>
        public bool DisableOutputOnExit { get; set; } = true;
    }

    /// <summary>
    /// Verifies a DC voltage source — a calibrator or a power supply, wrapped as an
    /// <see cref="IVoltageSourceDut"/> — by reading each programmed point back through a DC-voltage
    /// reference (any <see cref="DmmVoltageReference"/>-wrapped DMM), computing absolute and ppm-of-reading
    /// error and a PASS/FAIL verdict. This is the source-agnostic generalization of the Fluke-5440 runner:
    /// the same plan/result/CSV model (<see cref="VerificationPoint"/> / <see cref="VerificationResult"/>)
    /// applies, and the reference DMM is selectable.
    /// </summary>
    public sealed class DcSourceVerifier
    {
        private readonly IVoltageSourceDut _dut;
        private readonly IReferenceMeasurement _voltRef;
        private readonly DcSourceOptions _options;

        /// <summary>Optional diagnostic sink (progress). Null = silent.</summary>
        public Action<string> Log { get; set; }

        /// <summary>The DC-voltage reference in use; its <c>DisplayName</c> names the DMM.</summary>
        public IReferenceMeasurement Reference => _voltRef;

        public DcSourceVerifier(IVoltageSourceDut dut, IReferenceMeasurement voltageReference, DcSourceOptions options = null)
        {
            _dut = dut ?? throw new ArgumentNullException(nameof(dut));
            _voltRef = voltageReference ?? throw new ArgumentNullException(nameof(voltageReference));
            if (_voltRef.Quantity != ReferenceQuantity.DcVolts)
                throw new ArgumentException("Reference must measure DC volts.", nameof(voltageReference));
            _options = options ?? new DcSourceOptions();
        }

        /// <summary>
        /// Runs the whole plan and returns a result per point. <paramref name="sleep"/> is the settle-delay
        /// hook (ms). The DUT output is disabled afterwards when
        /// <see cref="DcSourceOptions.DisableOutputOnExit"/> is set.
        /// </summary>
        public IReadOnlyList<VerificationResult> Run(IReadOnlyList<VerificationPoint> plan, Action<int> sleep = null)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            sleep = sleep ?? (_ => { });

            var results = new List<VerificationResult>(plan.Count);
            try
            {
                for (int i = 0; i < plan.Count; i++)
                    results.Add(MeasurePoint(i + 1, plan[i], sleep));
            }
            finally
            {
                if (_options.DisableOutputOnExit)
                {
                    try { _dut.DisableOutput(); } catch (Exception ex) { Log?.Invoke("output-off failed: " + ex.Message); }
                }
            }
            return results;
        }

        private VerificationResult MeasurePoint(int index, VerificationPoint point, Action<int> sleep)
        {
            _dut.SetVolts(point.NominalVolts);
            _dut.EnableOutput();
            Log?.Invoke($"point {index}: {point.NominalVolts:G7} V, settling {_options.SettlingMs} ms");
            if (_options.SettlingMs > 0) sleep(_options.SettlingMs);

            _voltRef.Prepare(new ReferencePoint { FrequencyMHz = 0, NominalLevel = point.NominalVolts });

            int n = Math.Max(1, _options.Samples);
            var samples = new double[n];
            for (int s = 0; s < n; s++) samples[s] = _voltRef.Measure();
            var stats = DmmStatistics.Of(samples);

            double measured = stats.Average;
            double absErr = measured - point.NominalVolts;
            double ppmRdg = point.NominalVolts == 0 ? double.NaN : (absErr / point.NominalVolts) * 1e6;

            double? tol = point.TolerancePpm ?? _options.DefaultTolerancePpm;
            string verdict = null;
            if (tol.HasValue)
            {
                double fs = ResolveFullScale(point);
                double ppmCompare = point.NominalVolts == 0 ? Math.Abs(absErr / fs) * 1e6 : Math.Abs(ppmRdg);
                verdict = ppmCompare <= tol.Value ? "PASS" : "FAIL";
            }

            return new VerificationResult
            {
                Index = index,
                NominalVolts = point.NominalVolts,
                Range = point.Range ?? "AUTO",
                MeasuredVolts = measured,
                AbsErrorVolts = absErr,
                PpmOfReading = ppmRdg,
                StdDevVolts = stats.StdDev,
                Samples = n,
                TolerancePpm = tol,
                Verdict = verdict,
                Notes = point.Notes
            };
        }

        private double ResolveFullScale(VerificationPoint point)
        {
            if (TryParseVolts(point.Range, out var v) && v > 0) return v;
            return _options.ZeroFullScaleVolts;
        }

        private static bool TryParseVolts(string range, out double v)
        {
            v = 0;
            if (string.IsNullOrWhiteSpace(range) || range.Equals("AUTO", StringComparison.OrdinalIgnoreCase)) return false;
            return double.TryParse(range, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        }
    }
}
