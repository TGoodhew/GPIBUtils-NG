namespace GpibUtils.Instruments.Counters
{
    /// <summary>
    /// The shared shape of the legacy HP mnemonic microwave frequency counters (5342A, 5343A, 5351A). These
    /// have a single active input (selected by a mode/range code, not a numbered channel), no remote
    /// input-impedance control, and none of the modern universal-counter measurement model — so they cannot
    /// implement <see cref="IFrequencyCounter"/> (whose numbered-channel + selectable-impedance shape fits the
    /// SCPI 53131A instead). This narrow interface unifies the family so a caller can identify, initialize and
    /// read a microwave frequency uniformly; model-specific controls (manual center frequency, resolution,
    /// range, sample mode, oven/reference status) stay on the concrete drivers. New interface for issue #93.
    /// </summary>
    public interface ILegacyFrequencyCounter
    {
        /// <summary>The resource string this counter's session was opened for.</summary>
        string ResourceName { get; }

        /// <summary>Identifies the instrument (these predate <c>*IDN?</c>; returns a descriptor).</summary>
        string Identify();

        /// <summary>Device clear + preset to a known measuring state.</summary>
        void Initialize();

        /// <summary>Reads the measured frequency, in Hz (the counter talks its latest reading).</summary>
        double ReadFrequency();
    }
}
