namespace GpibUtils.Instruments.Meters
{
    /// <summary>
    /// The audio-distortion measurement surface of a DMM+THD analyzer (Keithley 2015/2015P) — THD, THD+N and
    /// SINAD over a configurable fundamental frequency and low/high measurement-bandwidth cutoffs. A companion
    /// to <see cref="IDigitalMultimeter"/> (the same instrument also implements that for its standard DMM
    /// functions); distortion / THD+N / SINAD do not fit the scalar one-shot DMM reading model. Runs over any
    /// <see cref="Visa.IInstrumentSession"/>. New interface for issue #94 (P1).
    /// </summary>
    public interface IAudioDistortionAnalyzer
    {
        /// <summary>The resource string this analyzer's session was opened for.</summary>
        string ResourceName { get; }

        /// <summary>Selects the distortion function and type (<c>:SENS:FUNC 'DIST'</c>; <c>:SENS:DIST:TYPE</c>).</summary>
        void ConfigureDistortion(DistortionType type);

        /// <summary>Sets the fundamental frequency, in Hz (disables auto-fundamental; <c>:SENS:DIST:FREQ</c>).</summary>
        void SetFundamentalFrequency(double hertz);

        /// <summary>Enables and sets the low-cutoff (high-pass) frequency, in Hz (<c>:SENS:DIST:LCO</c>).</summary>
        void SetLowCutoff(double hertz);

        /// <summary>Enables and sets the high-cutoff (low-pass) frequency, in Hz (<c>:SENS:DIST:HCO</c>).</summary>
        void SetHighCutoff(double hertz);

        /// <summary>Triggers and reads one distortion measurement (<c>:READ?</c>). Units follow the type:
        /// percent for THD / THD+N, dB for SINAD.</summary>
        double MeasureDistortion();
    }

    /// <summary>Audio-distortion measurement type (the <c>:SENS:DIST:TYPE</c> selector).</summary>
    public enum DistortionType
    {
        /// <summary>Total harmonic distortion (<c>THD</c>).</summary>
        Thd,
        /// <summary>Total harmonic distortion + noise (<c>THDN</c>).</summary>
        ThdPlusNoise,
        /// <summary>Signal-to-noise-and-distortion (<c>SINAD</c>).</summary>
        Sinad
    }
}
