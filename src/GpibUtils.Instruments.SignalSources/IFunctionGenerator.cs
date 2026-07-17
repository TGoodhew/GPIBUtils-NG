namespace GpibUtils.Instruments.SignalSources
{
    /// <summary>
    /// A function / arbitrary-waveform generator (HP 33120A, HP 8116A, Rigol DG1000Z). Distinct from
    /// <see cref="ISignalSource"/> (which models an RF CW source in dBm / MHz terms): a function generator is
    /// shaped around waveform SHAPE, amplitude in Vpp and a DC offset, over an audio-to-tens-of-MHz range.
    /// Lives in the SignalSources project (issue #88, resolving the SignalSources-vs-Waveforms placement in
    /// favour of SignalSources). Multi-channel instruments expose channel selection on the concrete driver;
    /// this interface operates on the active output.
    /// </summary>
    public interface IFunctionGenerator
    {
        /// <summary>The resource string this generator's session was opened for.</summary>
        string ResourceName { get; }

        /// <summary>Identifies the instrument (<c>*IDN?</c> where supported, else a descriptor).</summary>
        string Identify();

        /// <summary>Device clear + a known output state.</summary>
        void Initialize();

        /// <summary>Selects the output waveform shape.</summary>
        void SetWaveform(FunctionWaveform waveform);

        /// <summary>Sets the output frequency, in Hz.</summary>
        void SetFrequencyHz(double hertz);

        /// <summary>Sets the output amplitude, in volts peak-to-peak.</summary>
        void SetAmplitudeVpp(double voltsPeakToPeak);

        /// <summary>Sets the DC offset, in volts.</summary>
        void SetOffsetVolts(double volts);

        /// <summary>Enables the output.</summary>
        void OutputOn();

        /// <summary>Disables the output.</summary>
        void OutputOff();
    }

    /// <summary>Output waveform shape. Not every generator supports every shape.</summary>
    public enum FunctionWaveform { Sine, Square, Triangle, Ramp, Pulse, Noise, Dc, Arbitrary }
}
