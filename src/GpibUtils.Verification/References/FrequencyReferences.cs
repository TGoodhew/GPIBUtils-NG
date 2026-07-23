using System;
using GpibUtils.Instruments.Counters;

namespace GpibUtils.Verification.References
{
    /// <summary>
    /// Frequency reference backed by a universal counter (<see cref="IFrequencyCounter"/>, e.g. HP 53131A).
    /// Measures the DUT's frequency on a numbered input channel (default 1), returned in Hz.
    /// </summary>
    public sealed class FrequencyCounterReference : IReferenceMeasurement
    {
        private readonly IFrequencyCounter _counter;
        private readonly IDisposable _owned;
        private readonly int _channel;

        public FrequencyCounterReference(IFrequencyCounter counter, IDisposable ownedSession = null,
            string displayName = null, int channel = 1)
        {
            _counter = counter ?? throw new ArgumentNullException(nameof(counter));
            _owned = ownedSession;
            _channel = channel;
            DisplayName = displayName ?? "HP 53131A universal counter";
        }

        public string DisplayName { get; }
        public ReferenceQuantity Quantity => ReferenceQuantity.FrequencyHz;
        public string Unit => "Hz";

        public void Prepare(ReferencePoint point) { }

        public double Measure() => _counter.MeasureFrequency(_channel);

        public void Dispose() => _owned?.Dispose();
    }

    /// <summary>
    /// Frequency reference backed by a legacy mnemonic microwave counter
    /// (<see cref="ILegacyFrequencyCounter"/>: HP 5342A, 5343A, 5351A). Reads the counter's latest
    /// measured frequency in Hz.
    /// </summary>
    public sealed class LegacyCounterReference : IReferenceMeasurement
    {
        private readonly ILegacyFrequencyCounter _counter;
        private readonly IDisposable _owned;

        public LegacyCounterReference(ILegacyFrequencyCounter counter, IDisposable ownedSession = null,
            string displayName = null)
        {
            _counter = counter ?? throw new ArgumentNullException(nameof(counter));
            _owned = ownedSession;
            DisplayName = displayName ?? "microwave frequency counter";
        }

        public string DisplayName { get; }
        public ReferenceQuantity Quantity => ReferenceQuantity.FrequencyHz;
        public string Unit => "Hz";

        public void Prepare(ReferencePoint point) { }

        public double Measure() => _counter.ReadFrequency();

        public void Dispose() => _owned?.Dispose();
    }
}
