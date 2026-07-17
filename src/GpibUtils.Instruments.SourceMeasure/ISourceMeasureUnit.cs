using System.Globalization;

namespace GpibUtils.Instruments.SourceMeasure
{
    /// <summary>
    /// A source-measure unit (Keithley 2400 family): sources a voltage or current with a compliance limit and
    /// measures back voltage / current / resistance in one operation. Distinct from <c>IDcPowerSupply</c>
    /// (source-only) and the Meters interfaces (measure-only). Runs over any
    /// <see cref="Visa.IInstrumentSession"/>. New interface for issue #134 (P1 #84).
    /// </summary>
    public interface ISourceMeasureUnit
    {
        /// <summary>The resource string this SMU's session was opened for.</summary>
        string ResourceName { get; }

        /// <summary>Reads the instrument identity (<c>*IDN?</c>).</summary>
        string Identify();

        /// <summary>Device clear + reset to a known state.</summary>
        void Initialize();

        /// <summary>Instrument reset (<c>*RST</c>).</summary>
        void Reset();

        /// <summary>Selects whether the SMU sources voltage or current.</summary>
        void SetSourceFunction(SmuSourceFunction function);

        /// <summary>Sets the source output level (volts when sourcing voltage, amps when sourcing current).</summary>
        void SetSourceLevel(double value);

        /// <summary>Sets the compliance limit (a current limit when sourcing voltage, a voltage limit when
        /// sourcing current).</summary>
        void SetCompliance(double limit);

        /// <summary>Enables or disables the output.</summary>
        void SetOutput(bool on);

        /// <summary>Triggers a source-measure operation and returns the measured voltage / current / resistance.</summary>
        SmuReading Measure();
    }

    /// <summary>Which quantity the SMU sources.</summary>
    public enum SmuSourceFunction { Voltage, Current }

    /// <summary>A single SMU measurement: voltage (V), current (A), resistance (Ω).</summary>
    public struct SmuReading
    {
        public double Voltage { get; }
        public double Current { get; }
        public double Resistance { get; }

        public SmuReading(double voltage, double current, double resistance)
        {
            Voltage = voltage;
            Current = current;
            Resistance = resistance;
        }

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "V={0:G6}, I={1:G6}, R={2:G6}", Voltage, Current, Resistance);
    }
}
