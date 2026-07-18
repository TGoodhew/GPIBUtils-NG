namespace GpibUtils.Instruments.ElectronicLoads
{
    /// <summary>Regulation mode of a programmable DC electronic load.</summary>
    public enum LoadMode
    {
        /// <summary>Constant current — sinks a fixed current regardless of terminal voltage.</summary>
        ConstantCurrent,
        /// <summary>Constant voltage — holds the terminal at a fixed voltage.</summary>
        ConstantVoltage,
        /// <summary>Constant resistance — presents a fixed resistance.</summary>
        ConstantResistance,
        /// <summary>Constant power — sinks a fixed power.</summary>
        ConstantPower
    }

    /// <summary>
    /// A programmable DC electronic load (Maynuo M9811 / M97xx). A load <b>sinks</b> power under a chosen
    /// regulation mode (CC/CV/CR/CW) and reports the measured terminal voltage/current/power — a shape no
    /// existing interface expresses (`ISignalSource`/`IUniversalSource` source RF/DC, `IDcPowerSupply` sources
    /// power, `ISourceMeasureUnit` sources-and-measures). Runs over any <see cref="Visa.IInstrumentSession"/>.
    /// New interface for issue #164.
    /// </summary>
    public interface IElectronicLoad
    {
        /// <summary>The resource string this load's session was opened for.</summary>
        string ResourceName { get; }

        /// <summary>Identifies the instrument.</summary>
        string Identify();

        /// <summary>Puts the load under remote control and into a known state.</summary>
        void Initialize();

        /// <summary>Selects the regulation mode and its setpoint (amps for CC, volts for CV, ohms for CR,
        /// watts for CW).</summary>
        void SetMode(LoadMode mode, double setpoint);

        /// <summary>Enables the load input (starts sinking).</summary>
        void InputOn();

        /// <summary>Disables the load input (stops sinking).</summary>
        void InputOff();

        /// <summary>Reads the measured terminal voltage, in volts.</summary>
        double ReadVoltage();

        /// <summary>Reads the measured sink current, in amps.</summary>
        double ReadCurrent();

        /// <summary>Reads the sink power, in watts.</summary>
        double ReadPower();
    }
}
