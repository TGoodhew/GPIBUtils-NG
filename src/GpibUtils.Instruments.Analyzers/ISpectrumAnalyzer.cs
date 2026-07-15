using System.Collections.Generic;

namespace GpibUtils.Instruments.Analyzers
{
    /// <summary>
    /// A swept-tuned spectrum analyzer (HP 8560-series). Sets the frequency span, triggers a single sweep
    /// with an SRQ / operation-complete handshake, reads the trace, and reads the marker, over any
    /// <see cref="Visa.IInstrumentSession"/> — so the same driver runs on NI-VISA, a Prologix/AR488 adapter,
    /// or the in-memory simulator.
    /// </summary>
    public interface ISpectrumAnalyzer
    {
        /// <summary>The resource string this driver's session was opened for.</summary>
        string ResourceName { get; }

        /// <summary>Reads the instrument identity (the 8560-series <c>ID?</c> query).</summary>
        string Identify();

        /// <summary>Device clear + instrument preset to a known state.</summary>
        void Initialize();

        /// <summary>Instrument preset (<c>IP</c>).</summary>
        void Preset();

        /// <summary>Sets the center frequency, in Hz.</summary>
        void SetCenterFrequencyHz(double hertz);

        /// <summary>Sets the frequency span, in Hz (0 = zero span).</summary>
        void SetSpanHz(double hertz);

        /// <summary>Triggers a single sweep and blocks until the operation-complete SRQ fires. Throws if the
        /// sweep does not complete (timeout) or the analyzer signals an error.</summary>
        void SingleSweep();

        /// <summary>Reads the current trace amplitudes (the analyzer's <c>TRA?</c> array).</summary>
        IReadOnlyList<double> ReadTrace();

        /// <summary>Puts the marker on the highest peak and returns its amplitude (analyzer amplitude units).</summary>
        double MarkerToPeakAmplitude();

        /// <summary>Reads the active marker's frequency, in Hz.</summary>
        double MarkerFrequencyHz();
    }
}
