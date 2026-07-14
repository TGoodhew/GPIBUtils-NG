namespace GpibUtils.Instruments.Counters
{
    /// <summary>Input impedance of a counter channel.</summary>
    public enum CounterInputImpedance
    {
        /// <summary>50 Ω — for signals from 50 Ω sources (typical RF).</summary>
        Ohms50,

        /// <summary>1 MΩ — high impedance, to minimise loading of a high-impedance source.</summary>
        Ohms1M
    }

    /// <summary>
    /// A universal frequency counter (HP 53131A). Measures frequency on a numbered input channel with an
    /// SRQ / operation-complete handshake for measurement completion, over any
    /// <see cref="Visa.IInstrumentSession"/> — so the same driver runs on NI-VISA, a Prologix/AR488
    /// adapter, or the in-memory simulator.
    /// </summary>
    public interface IFrequencyCounter
    {
        /// <summary>The resource string this driver's session was opened for.</summary>
        string ResourceName { get; }

        /// <summary>Reads the instrument identity (<c>*IDN?</c>).</summary>
        string Identify();

        /// <summary>Device clear + reset to a known state (<c>*RST</c> / <c>*CLS</c> / status preset).</summary>
        void Initialize();

        /// <summary>Instrument reset (<c>*RST</c>).</summary>
        void Reset();

        /// <summary>
        /// Configures the given input channel (1–3) for a frequency measurement, triggers it, and returns
        /// the measured frequency in Hz once the operation-complete handshake fires. Throws
        /// <see cref="Hp53131AException"/> if the measurement does not complete (e.g. no/low signal).
        /// </summary>
        double MeasureFrequency(int channel);

        /// <summary>Sets the input impedance for the counter's channel 1 input.</summary>
        void SetInputImpedance(CounterInputImpedance impedance);
    }
}
