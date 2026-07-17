using System.Collections.Generic;

namespace GpibUtils.Instruments.NetworkAnalyzers
{
    /// <summary>
    /// A vector or scalar network analyzer (HP 8711C-8714C, 8720C, 8757D). Sets the frequency sweep and
    /// source power, selects the measured parameter, triggers a single sweep (blocking for completion — a
    /// hard requirement on these instruments before reading data), reads the formatted trace, and reads the
    /// peak marker. Scalar-first and vector-extensible: the <see cref="NetworkParameter"/> enum covers both
    /// S-parameters (vector) and the raw A/B/R inputs (scalar). Runs over any
    /// <see cref="Visa.IInstrumentSession"/>. New interface for issue #82 (P1).
    /// </summary>
    public interface INetworkAnalyzer
    {
        /// <summary>The resource string this analyzer's session was opened for.</summary>
        string ResourceName { get; }

        /// <summary>Reads the instrument identity.</summary>
        string Identify();

        /// <summary>Device clear + instrument preset.</summary>
        void Initialize();

        /// <summary>Sets the sweep start frequency, in Hz.</summary>
        void SetStartFrequencyHz(double hertz);

        /// <summary>Sets the sweep stop frequency, in Hz.</summary>
        void SetStopFrequencyHz(double hertz);

        /// <summary>Sets the source output power, in dBm.</summary>
        void SetSourcePowerDbm(double dbm);

        /// <summary>Sets the number of sweep points.</summary>
        void SetSweepPoints(int points);

        /// <summary>Selects the measured parameter (an S-parameter, or a raw A/B/R input on a scalar analyzer).</summary>
        void SetMeasurement(NetworkParameter parameter);

        /// <summary>Triggers one sweep and blocks until it completes (mandatory before any data/marker read).</summary>
        void SingleSweep();

        /// <summary>Reads the formatted (post-display-format) trace as an array of values.</summary>
        IReadOnlyList<double> ReadFormattedTrace();

        /// <summary>Moves the marker to the trace peak and returns its Y value (formatted units, e.g. dB).</summary>
        double MarkerToPeakY();

        /// <summary>Reads the active marker's stimulus frequency, in Hz.</summary>
        double MarkerFrequencyHz();
    }

    /// <summary>Measured parameter: an S-parameter (vector analyzers) or a raw receiver input (scalar).</summary>
    public enum NetworkParameter { S11, S21, S12, S22, InputA, InputB, InputR }
}
