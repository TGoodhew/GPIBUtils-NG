namespace GpibUtils.Instruments.LcrMeters
{
    /// <summary>
    /// A bridge-type impedance / LCR meter (HP 4275A): selects a primary measurement parameter (L/C/R/|Z|),
    /// a test frequency and circuit mode, then triggers a measurement and reads back a primary + secondary
    /// value (e.g. C and D, or L and Q). No existing category fits a component analyzer, so this is a new
    /// interface/category. Runs over any <see cref="Visa.IInstrumentSession"/>. New for issue #109 (P1 #83).
    /// </summary>
    public interface ILcrMeter
    {
        /// <summary>The resource string this driver's session was opened for.</summary>
        string ResourceName { get; }

        /// <summary>Identifies the instrument (the 4275A has no <c>*IDN?</c>; returns a descriptor).</summary>
        string Identify();

        /// <summary>Device clear + a known measurement state.</summary>
        void Initialize();

        /// <summary>Selects the primary (Display A) parameter — L / C / R / |Z|.</summary>
        void SetPrimaryParameter(LcrParameter parameter);

        /// <summary>Selects the test frequency (one of the instrument's spot frequencies).</summary>
        void SetTestFrequency(LcrFrequency frequency);

        /// <summary>Selects the equivalent-circuit mode (auto / series / parallel).</summary>
        void SetCircuitMode(LcrCircuitMode mode);

        /// <summary>Triggers one measurement (data-ready SRQ handshake) and returns the primary + secondary
        /// readings.</summary>
        LcrReading Measure();

        /// <summary>Performs an OPEN (zero) correction.</summary>
        void ZeroOpen();

        /// <summary>Performs a SHORT correction.</summary>
        void ZeroShort();
    }

    /// <summary>Primary (Display A) measurement parameter (the <c>A1</c>…<c>A4</c> program codes).</summary>
    public enum LcrParameter { Inductance = 1, Capacitance = 2, Resistance = 3, ImpedanceMagnitude = 4 }

    /// <summary>Equivalent-circuit mode (the <c>C1</c>/<c>C2</c>/<c>C3</c> program codes).</summary>
    public enum LcrCircuitMode { Auto = 1, Series = 2, Parallel = 3 }

    /// <summary>The 4275A's ten spot test frequencies (the <c>F11</c>…<c>F20</c> program codes).</summary>
    public enum LcrFrequency
    {
        F10kHz = 11, F20kHz = 12, F40kHz = 13, F100kHz = 14, F200kHz = 15,
        F400kHz = 16, F1MHz = 17, F2MHz = 18, F4MHz = 19, F10MHz = 20
    }

    /// <summary>A single LCR measurement: the primary (Display A) and secondary (Display B) values.</summary>
    public struct LcrReading
    {
        public double Primary { get; }
        public double Secondary { get; }

        public LcrReading(double primary, double secondary)
        {
            Primary = primary;
            Secondary = secondary;
        }

        public override string ToString() =>
            "primary=" + Primary.ToString("G6", System.Globalization.CultureInfo.InvariantCulture) +
            ", secondary=" + Secondary.ToString("G6", System.Globalization.CultureInfo.InvariantCulture);
    }
}
