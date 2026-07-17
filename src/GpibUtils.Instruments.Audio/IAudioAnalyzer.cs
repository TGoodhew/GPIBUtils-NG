namespace GpibUtils.Instruments.Audio
{
    /// <summary>
    /// A stand-alone audio analyzer (HP 8903B): a combined audio source, AC/DC voltmeter, notch-filter
    /// distortion analyzer (THD/THD+N/SINAD) and frequency counter in one box. None of the existing categories
    /// fit, so this is a new interface/category. Runs over any <see cref="Visa.IInstrumentSession"/>. New for
    /// issue #131 (P1 #86).
    /// </summary>
    public interface IAudioAnalyzer
    {
        /// <summary>The resource string this driver's session was opened for.</summary>
        string ResourceName { get; }

        /// <summary>Identifies the instrument (the 8903B has no <c>*IDN?</c>; returns a descriptor).</summary>
        string Identify();

        /// <summary>Device clear + Automatic Operation reset (clears special functions) — a known state.</summary>
        void Initialize();

        /// <summary>Sets the internal source frequency, in Hz (<c>FR &lt;hz&gt; HZ</c>).</summary>
        void SetSourceFrequencyHz(double hertz);

        /// <summary>Sets the internal source amplitude, in the given unit (<c>AP &lt;value&gt; &lt;unit&gt;</c>).</summary>
        void SetSourceAmplitude(double value, AudioAmplitudeUnit unit);

        /// <summary>Selects the measurement function (<c>M1</c>/<c>M2</c>/<c>M3</c>/<c>S1</c>/<c>S2</c>/<c>S3</c>).</summary>
        void SetMeasurement(AudioMeasurement measurement);

        /// <summary>Selects the detector (<c>A0</c> RMS / <c>A1</c> average).</summary>
        void SetDetector(AudioDetector detector);

        /// <summary>Arms Hold, enables Data-Ready SRQ, triggers a settled measurement, waits for the SRQ, and
        /// reads the result.</summary>
        double Measure();
    }

    /// <summary>Measurement function (the <c>M</c>/<c>S</c> program codes).</summary>
    public enum AudioMeasurement
    {
        AcLevel,            // M1
        Sinad,              // M2
        Distortion,         // M3
        DcLevel,            // S1
        SignalToNoise,      // S2
        DistortionLevel     // S3
    }

    /// <summary>Detector selection (the <c>A0</c>/<c>A1</c> program codes).</summary>
    public enum AudioDetector { Rms = 0, Average = 1 }

    /// <summary>Source amplitude unit (the amplitude-suffix program codes).</summary>
    public enum AudioAmplitudeUnit { Volts, Millivolts, Dbm }
}
