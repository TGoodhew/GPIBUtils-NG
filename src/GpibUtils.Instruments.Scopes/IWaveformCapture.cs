namespace GpibUtils.Instruments.Scopes
{
    /// <summary>
    /// Opt-in raw-waveform transfer for a scope that supports it (the standard SCPI <c>:WAVeform</c> subsystem
    /// on the Rigol and Agilent/Keysight families). An acquire-then-transfer operation: it gates on acquisition
    /// completion, then returns the captured samples. A companion to <see cref="IOscilloscope"/> — the binary
    /// waveform formats on the Tektronix (<c>CURVe?</c>) and LeCroy (<c>WAVEFORM?</c>/WAVEDESC) dialects are a
    /// separate follow-up, so those drivers do not implement this interface yet. New interface for issue #95.
    /// </summary>
    public interface IWaveformCapture
    {
        /// <summary>Acquires and returns the channel's waveform as vertical sample values (volts). Blocks until
        /// the acquisition completes.</summary>
        double[] CaptureWaveform(int channel);
    }
}
