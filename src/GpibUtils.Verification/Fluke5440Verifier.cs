using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Instruments.Calibrators;
using GpibUtils.Instruments.Meters;

namespace GpibUtils.Verification
{
    /// <summary>
    /// Non-interactive verification runner: drives a Fluke 5440A calibrator through a plan of nominal output
    /// voltages and reads each back through an HP/Agilent/Keysight 34401A DMM, computing the absolute and
    /// ppm-of-reading error and (when a tolerance is given) a PASS/FAIL verdict. Ported from the standalone
    /// <c>5440Verify</c> app (issue #37); the companion <c>34401AController</c> menu owns the same workflow.
    ///
    /// <para>The DMM is configured once at the top of the run (FUNC/range/NPLC/autozero/input-Z/trigger) so
    /// per-point settings stay sticky; each point sets the 5440 output, goes to Operate, settles, then
    /// averages N DMM reads. The 5440 is returned to Standby when the run ends
    /// (<see cref="VerificationOptions.StandbyOnExit"/>).</para>
    /// </summary>
    public sealed class Fluke5440Verifier
    {
        private readonly Fluke5440A _cal;
        private readonly Hp34401A _dmm;
        private readonly VerificationOptions _options;

        /// <summary>Optional diagnostic sink (bus echo / progress). Null = silent.</summary>
        public Action<string> Log { get; set; }

        public Fluke5440Verifier(Fluke5440A calibrator, Hp34401A dmm, VerificationOptions options)
        {
            _cal = calibrator ?? throw new ArgumentNullException(nameof(calibrator));
            _dmm = dmm ?? throw new ArgumentNullException(nameof(dmm));
            _options = options ?? new VerificationOptions();
        }

        /// <summary>Applies the once-per-run DMM + 5440 configuration.</summary>
        public void Configure()
        {
            _dmm.Configure(MeasurementFunction.DcVoltage);
            ApplyRange(_options.GlobalRange);
            _dmm.SetNplc(MeasurementFunction.DcVoltage, _options.Nplc);
            _dmm.SetAutoZero(_options.Autozero);
            _dmm.SetInputImpedanceAuto(_options.HighImpedance);
            _dmm.SetTriggerSource(TriggerSource.Immediate);

            _cal.SetSenseMode(_options.SenseExternal ? CalibratorSenseMode.ExternalFourWire : CalibratorSenseMode.InternalTwoWire);
        }

        private void ApplyRange(string range)
        {
            if (string.IsNullOrWhiteSpace(range) || range.Equals("AUTO", StringComparison.OrdinalIgnoreCase))
                _dmm.SetAutoRange(MeasurementFunction.DcVoltage, true);
            else
                _dmm.SetRange(MeasurementFunction.DcVoltage, range);
        }

        /// <summary>
        /// Runs the whole plan and returns a result per point. <paramref name="sleep"/> is the settle delay
        /// hook (ms); pass <c>Thread.Sleep</c> at the bench or a no-op in tests. The 5440 is returned to
        /// Standby afterwards when <see cref="VerificationOptions.StandbyOnExit"/> is set.
        /// </summary>
        public IReadOnlyList<VerificationResult> Run(IReadOnlyList<VerificationPoint> plan, Action<int> sleep = null)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            sleep = sleep ?? (_ => { });

            var results = new List<VerificationResult>(plan.Count);
            try
            {
                Configure();
                for (int i = 0; i < plan.Count; i++)
                    results.Add(MeasurePoint(i + 1, plan[i], sleep));
            }
            finally
            {
                if (_options.StandbyOnExit)
                {
                    try { _cal.Standby(); } catch (Exception ex) { Log?.Invoke("STBY failed: " + ex.Message); }
                }
            }
            return results;
        }

        private VerificationResult MeasurePoint(int index, VerificationPoint point, Action<int> sleep)
        {
            if (point.Range != null) ApplyRange(point.Range);

            _cal.SetOutputVolts(point.NominalVolts);
            _cal.Operate();
            Log?.Invoke($"point {index}: {point.NominalVolts:G7} V, settling {_options.SettlingMs} ms");
            if (_options.SettlingMs > 0) sleep(_options.SettlingMs);

            int n = Math.Max(1, _options.Samples);
            var samples = new double[n];
            double sum = 0;
            for (int s = 0; s < n; s++)
            {
                double x = _dmm.ReadValue();
                samples[s] = x;
                sum += x;
            }

            double measured = sum / n;
            double stddev = n > 1 ? PopulationStdDev(samples, measured) : 0.0;
            double absErr = measured - point.NominalVolts;
            double ppmRdg = point.NominalVolts == 0 ? double.NaN : (absErr / point.NominalVolts) * 1e6;

            double? tol = point.TolerancePpm ?? _options.DefaultTolerancePpm;
            string verdict = null;
            if (tol.HasValue)
            {
                // ppm-of-reading is meaningless at 0 V; compare against the full-scale of the effective range.
                double fs = ResolveFullScale(point);
                double ppmCompare = point.NominalVolts == 0 ? Math.Abs(absErr / fs) * 1e6 : Math.Abs(ppmRdg);
                verdict = ppmCompare <= tol.Value ? "PASS" : "FAIL";
            }

            return new VerificationResult
            {
                Index = index,
                NominalVolts = point.NominalVolts,
                Range = point.Range ?? _options.GlobalRange ?? "AUTO",
                MeasuredVolts = measured,
                AbsErrorVolts = absErr,
                PpmOfReading = ppmRdg,
                StdDevVolts = stddev,
                Samples = n,
                TolerancePpm = tol,
                Verdict = verdict,
                Notes = point.Notes
            };
        }

        private double ResolveFullScale(VerificationPoint point)
        {
            if (TryParseVolts(point.Range, out var v) && v > 0) return v;
            if (TryParseVolts(_options.GlobalRange, out v) && v > 0) return v;
            return 10.0;
        }

        private static bool TryParseVolts(string range, out double v)
        {
            v = 0;
            if (string.IsNullOrWhiteSpace(range) || range.Equals("AUTO", StringComparison.OrdinalIgnoreCase)) return false;
            return double.TryParse(range, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        }

        internal static double PopulationStdDev(IReadOnlyList<double> xs, double mean)
        {
            double s = 0;
            foreach (var x in xs) { double d = x - mean; s += d * d; }
            return Math.Sqrt(s / xs.Count);
        }
    }
}
