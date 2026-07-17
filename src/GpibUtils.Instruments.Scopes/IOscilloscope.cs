namespace GpibUtils.Instruments.Scopes
{
    /// <summary>
    /// A digital oscilloscope (Rigol DS1054Z, Tektronix/Agilent/LeCroy families, …). Run/stop/single
    /// acquisition control, per-channel display, and parameterized automatic measurements, over any
    /// <see cref="Visa.IInstrumentSession"/>. Raw waveform transfer is the separate opt-in
    /// <see cref="IWaveformCapture"/> (not every scope dialect exposes it uniformly).
    /// </summary>
    public interface IOscilloscope
    {
        /// <summary>The resource string this scope's session was opened for.</summary>
        string ResourceName { get; }

        /// <summary>Reads the instrument identity (<c>*IDN?</c>).</summary>
        string Identify();

        /// <summary>Device clear + a clean known state.</summary>
        void Initialize();

        /// <summary>Starts continuous acquisition (<c>:RUN</c>).</summary>
        void Run();

        /// <summary>Stops acquisition (<c>:STOP</c>).</summary>
        void Stop();

        /// <summary>Arms a single acquisition (<c>:SINGle</c>).</summary>
        void Single();

        /// <summary>Auto-scales the display (<c>:AUToscale</c>).</summary>
        void AutoScale();

        /// <summary>Turns a channel's display trace on or off.</summary>
        void SetChannelDisplay(int channel, bool on);

        /// <summary>Measures peak-to-peak voltage on a channel (volts) — shorthand for
        /// <see cref="Measure"/> with <see cref="ScopeMeasurementType.PeakToPeak"/>.</summary>
        double MeasureVpp(int channel);

        /// <summary>Takes an automatic measurement of the given type on a channel. Units follow the type:
        /// volts for the voltage measurements, Hz for <see cref="ScopeMeasurementType.Frequency"/>, seconds for
        /// period/rise/fall.</summary>
        double Measure(int channel, ScopeMeasurementType type);
    }

    /// <summary>An automatic-measurement quantity common to the supported scope dialects (each maps it to its
    /// own measurement keyword).</summary>
    public enum ScopeMeasurementType
    {
        /// <summary>Peak-to-peak voltage.</summary>
        PeakToPeak,
        /// <summary>Maximum voltage.</summary>
        Maximum,
        /// <summary>Minimum voltage.</summary>
        Minimum,
        /// <summary>Amplitude (top − base).</summary>
        Amplitude,
        /// <summary>Mean (average) voltage.</summary>
        Mean,
        /// <summary>RMS voltage.</summary>
        Rms,
        /// <summary>Frequency (Hz).</summary>
        Frequency,
        /// <summary>Period (seconds).</summary>
        Period,
        /// <summary>Rise time (seconds).</summary>
        RiseTime,
        /// <summary>Fall time (seconds).</summary>
        FallTime
    }
}
