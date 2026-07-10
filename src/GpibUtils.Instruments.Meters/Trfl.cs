namespace GpibUtils.Instruments.Meters
{
    /// <summary>
    /// Which 8902A IF detector a Tuned RF Level measurement uses. The choice sets both the noise
    /// bandwidth and the usable depth (8902A O&amp;C, Tuned RF Level ranges):
    /// <list type="bullet">
    /// <item><b>Average</b> (SF 4.4, 30 kHz BW) — floor ≈ −100 dBm; tolerant of a noisy / drifting
    /// source (residual FM), so it holds through an LO + converter path. This is the default.</item>
    /// <item><b>Synchronous</b> (SF 4.0, 200 Hz BW) — floor ≈ −127 dBm, the depth needed to reach a
    /// full 110 dB. Its narrow band needs a spectrally clean signal, so it can lose lock (Error 96)
    /// through the converter path — used for the deep sweep.</item>
    /// </list>
    /// </summary>
    public enum TrflDetector
    {
        /// <summary>IF Average detector (SF 4.4, 30 kHz BW, floor ≈ −100 dBm).</summary>
        Average,

        /// <summary>IF Synchronous detector (SF 4.0, 200 Hz BW, floor ≈ −127 dBm).</summary>
        Synchronous
    }

    /// <summary>
    /// How the receiver tunes to the signal for a Tuned RF Level measurement.
    /// <list type="bullet">
    /// <item><b>Manual</b> — enter the frequency directly (<c>&lt;freq&gt;MZ</c>); the receiver tunes to
    /// exactly that frequency and holds it. Fast and deterministic when the frequency is known. The default.</item>
    /// <item><b>Auto</b> — let the receiver search for and acquire the signal, then drop to manual tune to
    /// hold it, then re-enter TRFL. Useful when the frequency is uncertain or the source drifts.</item>
    /// </list>
    /// </summary>
    public enum TrflTuning
    {
        /// <summary>Manual tuning — <c>&lt;freq&gt;MZ</c>, tune to exactly the commanded frequency (default).</summary>
        Manual,

        /// <summary>Automatic tuning — search/acquire the signal, then hold it and re-enter TRFL.</summary>
        Auto
    }
}
