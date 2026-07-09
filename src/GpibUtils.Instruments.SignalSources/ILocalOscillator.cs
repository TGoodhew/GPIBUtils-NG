namespace GpibUtils.Instruments.SignalSources
{
    /// <summary>
    /// An external local oscillator (e.g. HP 8673B) — a <see cref="ISignalSource"/> that also advertises
    /// its tuning range, so callers using it as an LO can validate or plan frequencies.
    /// </summary>
    public interface ILocalOscillator : ISignalSource
    {
        /// <summary>Lowest frequency the generator can produce, in MHz.</summary>
        double MinFrequencyMHz { get; }

        /// <summary>Highest frequency the generator can produce, in MHz.</summary>
        double MaxFrequencyMHz { get; }
    }
}
