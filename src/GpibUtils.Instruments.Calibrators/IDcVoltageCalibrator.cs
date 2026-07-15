namespace GpibUtils.Instruments.Calibrators
{
    /// <summary>Output sensing mode of a DC voltage calibrator.</summary>
    public enum CalibratorSenseMode
    {
        /// <summary>External sense — remote 4-wire sensing at the load (<c>ESNS</c> on the 5440).</summary>
        ExternalFourWire,

        /// <summary>Internal sense — local 2-wire sensing at the calibrator terminals (<c>ISNS</c>).</summary>
        InternalTwoWire
    }

    /// <summary>Output state of a calibrator: connected to the terminals, or disconnected/zeroed.</summary>
    public enum CalibratorOutputState
    {
        /// <summary>Standby — output disconnected from the terminals (safe).</summary>
        Standby,

        /// <summary>Operate — programmed value present on the output terminals.</summary>
        Operate
    }

    /// <summary>
    /// A precision DC voltage calibrator (Fluke 5440A/5440B). Programs an output voltage, switches between
    /// Operate and Standby, and selects the sensing mode, over any <see cref="Visa.IInstrumentSession"/> —
    /// so the same driver runs on NI-VISA, a Prologix/AR488 adapter, or the in-memory simulator.
    ///
    /// <para>The 5440 predates IEEE-488.2 (no <c>*IDN?</c>); identity over the bus is limited to the firmware
    /// version (<c>GVRS</c>). All commands are the instrument's own mnemonics (<c>SOUT</c>, <c>OPER</c>,
    /// <c>STBY</c>, …).</para>
    /// </summary>
    public interface IDcVoltageCalibrator
    {
        /// <summary>The resource string this driver's session was opened for.</summary>
        string ResourceName { get; }

        /// <summary>A fixed descriptor (the 5440 has no <c>*IDN?</c>); use <see cref="FirmwareVersion"/> for
        /// the only identity available over the bus.</summary>
        string Identify();

        /// <summary>Reads the firmware version (<c>GVRS</c>) — the numeric mantissa the 5440 returns
        /// (e.g. "02.01"). Model and serial number are not retrievable over GPIB.</summary>
        string FirmwareVersion();

        /// <summary>Device clear + reset to the power-on state (<c>RESET</c>): standby, output cleared.</summary>
        void Initialize();

        /// <summary>Resets the calibrator to its power-on state (<c>RESET</c>).</summary>
        void Reset();

        /// <summary>Programs the DC output level in volts (<c>SOUT &lt;v&gt;</c>). The value takes effect on
        /// the terminals only in <see cref="CalibratorOutputState.Operate"/>.</summary>
        void SetOutputVolts(double volts);

        /// <summary>Reads the present programmed output level in volts (<c>GOUT</c>).</summary>
        double GetOutputVolts();

        /// <summary>Switches the output to Operate or Standby (<c>OPER</c> / <c>STBY</c>).</summary>
        void SetOutputState(CalibratorOutputState state);

        /// <summary>Selects external 4-wire (<c>ESNS</c>) or internal 2-wire (<c>ISNS</c>) sensing.</summary>
        void SetSenseMode(CalibratorSenseMode mode);
    }
}
