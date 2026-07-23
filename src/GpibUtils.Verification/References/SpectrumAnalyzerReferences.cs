using System;
using GpibUtils.Instruments.Analyzers;

namespace GpibUtils.Verification.References
{
    /// <summary>
    /// RF-power reference backed by a swept spectrum analyzer (<see cref="ISpectrumAnalyzer"/>: HP 8560E,
    /// 8591E). At each point it tunes the analyzer to the carrier, takes a single sweep, and reads the peak
    /// marker amplitude (dBm) — a lower-accuracy but perfectly valid alternative to a power meter / 8902A
    /// for checking a signal generator's output level, so the user can pick whichever they have on the bench.
    /// </summary>
    public sealed class SpectrumAnalyzerPowerReference : IReferenceMeasurement
    {
        private readonly ISpectrumAnalyzer _analyzer;
        private readonly IDisposable _owned;
        private readonly double _spanHz;

        public SpectrumAnalyzerPowerReference(ISpectrumAnalyzer analyzer, IDisposable ownedSession = null,
            string displayName = null, double spanHz = 1e6)
        {
            _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
            _owned = ownedSession;
            _spanHz = spanHz;
            DisplayName = displayName ?? "spectrum analyzer";
        }

        public string DisplayName { get; }
        public ReferenceQuantity Quantity => ReferenceQuantity.RfPowerDbm;
        public string Unit => "dBm";

        public void Prepare(ReferencePoint point)
        {
            if (point == null) throw new ArgumentNullException(nameof(point));
            _analyzer.SetCenterFrequencyHz(point.FrequencyMHz * 1e6);
            _analyzer.SetSpanHz(_spanHz);
        }

        public double Measure()
        {
            _analyzer.SingleSweep();
            return _analyzer.MarkerToPeakAmplitude();
        }

        public void Dispose() => _owned?.Dispose();
    }

    /// <summary>
    /// Frequency reference backed by a swept spectrum analyzer: tunes to the carrier, sweeps, peaks the
    /// marker and reads its frequency (Hz). Coarser than a counter (limited by span / bin width) but
    /// available when a counter isn't.
    /// </summary>
    public sealed class SpectrumAnalyzerFrequencyReference : IReferenceMeasurement
    {
        private readonly ISpectrumAnalyzer _analyzer;
        private readonly IDisposable _owned;
        private readonly double _spanHz;

        public SpectrumAnalyzerFrequencyReference(ISpectrumAnalyzer analyzer, IDisposable ownedSession = null,
            string displayName = null, double spanHz = 1e6)
        {
            _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
            _owned = ownedSession;
            _spanHz = spanHz;
            DisplayName = displayName ?? "spectrum analyzer";
        }

        public string DisplayName { get; }
        public ReferenceQuantity Quantity => ReferenceQuantity.FrequencyHz;
        public string Unit => "Hz";

        public void Prepare(ReferencePoint point)
        {
            if (point == null) throw new ArgumentNullException(nameof(point));
            _analyzer.SetCenterFrequencyHz(point.FrequencyMHz * 1e6);
            _analyzer.SetSpanHz(_spanHz);
        }

        public double Measure()
        {
            _analyzer.SingleSweep();
            _analyzer.MarkerToPeakAmplitude();   // peak the marker first
            return _analyzer.MarkerFrequencyHz();
        }

        public void Dispose() => _owned?.Dispose();
    }
}
