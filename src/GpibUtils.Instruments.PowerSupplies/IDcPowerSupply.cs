namespace GpibUtils.Instruments.PowerSupplies
{
    /// <summary>
    /// A programmable DC power supply (HP E3633A, Rigol DP832, …). Sets the output voltage and current
    /// limit, gates the output, and reads back the actual measured voltage/current, over any
    /// <see cref="Visa.IInstrumentSession"/> — so the same driver runs on NI-VISA, a Prologix/AR488
    /// adapter, or the in-memory simulator. Multi-output supplies (e.g. the DP832's three channels) expose
    /// per-channel selection on the concrete driver.
    /// </summary>
    public interface IDcPowerSupply
    {
        /// <summary>The resource string this driver's session was opened for.</summary>
        string ResourceName { get; }

        /// <summary>Reads the instrument identity (<c>*IDN?</c>).</summary>
        string Identify();

        /// <summary>Device clear + reset to a known state.</summary>
        void Initialize();

        /// <summary>Instrument reset (<c>*RST</c>).</summary>
        void Reset();

        /// <summary>Sets the programmed output voltage (volts).</summary>
        void SetVoltage(double volts);

        /// <summary>Sets the current limit (amps).</summary>
        void SetCurrentLimit(double amps);

        /// <summary>Enables or disables the output.</summary>
        void SetOutput(bool on);

        /// <summary>Measures the actual output voltage (volts).</summary>
        double MeasureVoltage();

        /// <summary>Measures the actual output current (amps).</summary>
        double MeasureCurrent();
    }
}
