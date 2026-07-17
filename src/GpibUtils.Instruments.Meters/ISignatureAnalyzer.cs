namespace GpibUtils.Instruments.Meters
{
    /// <summary>
    /// A logic <b>signature multimeter</b> (HP 5005B): a digital-signature troubleshooting analyzer that
    /// also does general multimeter functions (frequency, totalize, time-interval, resistance, DC/differential/
    /// peak voltage) over one shared function-select register and one SRQ-driven measurement pipeline. None of
    /// the existing <c>Meters</c> interfaces (<see cref="IMeasuringReceiver"/>, <see cref="IDigitalMultimeter"/>,
    /// <see cref="IPowerMeter"/>) fit this hybrid, so it gets its own interface. Runs over any
    /// <see cref="Visa.IInstrumentSession"/>. New interface for issue #112 (relates to P1 #92).
    /// </summary>
    public interface ISignatureAnalyzer
    {
        /// <summary>The resource string this driver's session was opened for.</summary>
        string ResourceName { get; }

        /// <summary>Reads the instrument identity (<c>ID</c> — the 5005B has no <c>*IDN?</c>).</summary>
        string Identify();

        /// <summary>Device clear + reset to power-up defaults (clean known state).</summary>
        void Initialize();

        /// <summary>Reset to power-up defaults (<c>RS</c>).</summary>
        void Reset();

        /// <summary>Selects the measurement function (<c>Fn</c>).</summary>
        void SetFunction(SignatureFunction function);

        /// <summary>Sets the DATA-channel logic threshold family (<c>TDn</c>).</summary>
        void SetDataThreshold(LogicThreshold threshold);

        /// <summary>Sets the CLOCK-channel active edge polarity (<c>PCn</c>).</summary>
        void SetClockPolarity(EdgePolarity polarity);

        /// <summary>Enables or disables the probe switch (<c>PSn</c>).</summary>
        void SetProbeSwitchEnabled(bool enabled);

        /// <summary>Arms the SRQ mask, waits for the measurement to complete (data-ready SRQ), and returns the
        /// raw ASCII reading — a 4-hex signature for the signature functions, or a number for the rest.</summary>
        string TriggerAndRead();

        /// <summary>Selects a numeric function, triggers a measurement, and returns the parsed value.</summary>
        double Measure(SignatureFunction function);

        /// <summary>Reads the decimal error code (<c>SE</c>); 0 = no error.</summary>
        int ReadErrorCode();
    }

    /// <summary>HP 5005B measurement functions (the <c>Fn</c> select codes, n = 0…9).</summary>
    public enum SignatureFunction
    {
        NormSignature = 0,
        QualSignature = 1,
        Frequency = 2,
        Totalize = 3,
        TimeInterval = 4,
        Resistance = 5,
        DcVoltage = 6,
        DifferentialVoltage = 7,
        PositivePeakVoltage = 8,
        NegativePeakVoltage = 9
    }

    /// <summary>Logic threshold family (the <c>n</c> in <c>TDn</c>/<c>TCn</c>/<c>TQn</c>).</summary>
    public enum LogicThreshold { Ttl = 1, Ecl = 2, Cmos5V = 3 }

    /// <summary>Active edge / level polarity (the <c>n</c> in <c>PCn</c>/<c>PTn</c>/<c>PPn</c>/<c>PQn</c>).</summary>
    public enum EdgePolarity { RisingOrHigh = 1, FallingOrLow = 2 }
}
