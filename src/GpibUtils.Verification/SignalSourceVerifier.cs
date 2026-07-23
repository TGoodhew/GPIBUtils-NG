using System;
using System.Collections.Generic;
using GpibUtils.Instruments.Meters;
using GpibUtils.Instruments.SignalSources;
using GpibUtils.Verification.References;

namespace GpibUtils.Verification
{
    /// <summary>
    /// Verifies a signal generator / CW source (any <see cref="ISignalSource"/>) against one or two
    /// reference measuring instruments: an RF-power reference (an HP 8902A measuring receiver or a power
    /// meter) and/or a frequency reference (a counter or the 8902A). For each plan point the source is set
    /// to the commanded frequency + power, RF is enabled, the point settles, then the reference(s) are
    /// averaged over <see cref="SignalSourceOptions.Samples"/> reads. Power error is graded in dB and
    /// frequency error in ppm; a point PASSes only when every graded quantity is within tolerance.
    ///
    /// <para>This is the generalization of the plan-driven 5440 runner to the RF domain: it is the model
    /// behind "use the 8902A to verify a signal generator", and because more than one instrument can fill
    /// each reference role the caller (or the interactive harness) picks which one to use.</para>
    /// </summary>
    public sealed class SignalSourceVerifier
    {
        private readonly ISignalSource _dut;
        private readonly IReferenceMeasurement _powerRef;
        private readonly IReferenceMeasurement _freqRef;
        private readonly SignalSourceOptions _options;

        /// <summary>Optional diagnostic sink (progress). Null = silent.</summary>
        public Action<string> Log { get; set; }

        /// <summary>The RF-power reference in use (may be null). Its <c>DisplayName</c> names the instrument.</summary>
        public IReferenceMeasurement PowerReference => _powerRef;

        /// <summary>The frequency reference in use (may be null).</summary>
        public IReferenceMeasurement FrequencyReference => _freqRef;

        public SignalSourceVerifier(ISignalSource dut, IReferenceMeasurement powerReference,
            IReferenceMeasurement frequencyReference, SignalSourceOptions options = null)
        {
            _dut = dut ?? throw new ArgumentNullException(nameof(dut));
            if (powerReference == null && frequencyReference == null)
                throw new ArgumentException("At least one reference (power and/or frequency) is required.");
            if (powerReference != null && powerReference.Quantity != ReferenceQuantity.RfPowerDbm)
                throw new ArgumentException("Power reference must measure RF power (dBm).", nameof(powerReference));
            if (frequencyReference != null && frequencyReference.Quantity != ReferenceQuantity.FrequencyHz)
                throw new ArgumentException("Frequency reference must measure frequency (Hz).", nameof(frequencyReference));

            _powerRef = powerReference;
            _freqRef = frequencyReference;
            _options = options ?? new SignalSourceOptions();
        }

        /// <summary>
        /// Runs the whole plan and returns a result per point. <paramref name="sleep"/> is the settle-delay
        /// hook (ms); pass <c>Thread.Sleep</c> at the bench or a no-op in tests. The source's RF output is
        /// turned off afterwards when <see cref="SignalSourceOptions.RfOffOnExit"/> is set.
        /// </summary>
        public IReadOnlyList<SignalSourceResult> Run(IReadOnlyList<SignalSourcePoint> plan, Action<int> sleep = null)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            sleep = sleep ?? (_ => { });

            var results = new List<SignalSourceResult>(plan.Count);
            try
            {
                for (int i = 0; i < plan.Count; i++)
                    results.Add(MeasurePoint(i + 1, plan[i], sleep));
            }
            finally
            {
                if (_options.RfOffOnExit)
                {
                    try { _dut.RfOff(); } catch (Exception ex) { Log?.Invoke("RF off failed: " + ex.Message); }
                }
            }
            return results;
        }

        private SignalSourceResult MeasurePoint(int index, SignalSourcePoint point, Action<int> sleep)
        {
            _dut.SetFrequencyMHz(point.FrequencyMHz);
            _dut.SetPowerDbm(point.PowerDbm);
            _dut.RfOn();
            Log?.Invoke($"point {index}: {point.FrequencyMHz:G9} MHz @ {point.PowerDbm:G4} dBm, settling {_options.SettlingMs} ms");
            if (_options.SettlingMs > 0) sleep(_options.SettlingMs);

            var ctx = new ReferencePoint { FrequencyMHz = point.FrequencyMHz, NominalLevel = point.PowerDbm };
            int n = Math.Max(1, _options.Samples);

            var result = new SignalSourceResult
            {
                Index = index,
                FrequencyMHz = point.FrequencyMHz,
                PowerDbm = point.PowerDbm,
                Samples = n,
                Notes = point.Notes
            };

            if (_powerRef != null)
            {
                var stats = Average(_powerRef, ctx, n);
                result.PowerMeasured = true;
                result.MeasuredPowerDbm = stats.Average;
                result.PowerErrorDb = stats.Average - point.PowerDbm;
                result.PowerStdDevDb = stats.StdDev;
                double? tolDb = point.PowerToleranceDb ?? _options.DefaultPowerToleranceDb;
                result.PowerToleranceDb = tolDb;
                if (tolDb.HasValue)
                    result.PowerVerdict = Math.Abs(result.PowerErrorDb) <= tolDb.Value ? "PASS" : "FAIL";
            }

            if (_freqRef != null)
            {
                var stats = Average(_freqRef, ctx, n);
                double targetHz = point.FrequencyMHz * 1e6;
                result.FrequencyMeasured = true;
                result.MeasuredFrequencyHz = stats.Average;
                result.FrequencyErrorHz = stats.Average - targetHz;
                result.FrequencyErrorPpm = targetHz == 0 ? double.NaN : (result.FrequencyErrorHz / targetHz) * 1e6;
                result.FrequencyTolerancePpm = point.FrequencyTolerancePpm ?? _options.DefaultFrequencyTolerancePpm;
                double? tolPpm = point.FrequencyTolerancePpm ?? _options.DefaultFrequencyTolerancePpm;
                if (tolPpm.HasValue && !double.IsNaN(result.FrequencyErrorPpm))
                    result.FrequencyVerdict = Math.Abs(result.FrequencyErrorPpm) <= tolPpm.Value ? "PASS" : "FAIL";
            }

            return result;
        }

        private static DmmStatistics Average(IReferenceMeasurement reference, ReferencePoint ctx, int n)
        {
            reference.Prepare(ctx);
            var samples = new double[n];
            for (int s = 0; s < n; s++) samples[s] = reference.Measure();
            return DmmStatistics.Of(samples);
        }
    }
}
