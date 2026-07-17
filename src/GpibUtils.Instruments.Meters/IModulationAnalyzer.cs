namespace GpibUtils.Instruments.Meters
{
    /// <summary>
    /// A modulation analyzer (HP 8901A/8901B): tunes to an RF carrier and measures its AM depth, FM/ΦM
    /// deviation, RF power/level, or frequency. Distinct from <c>IMeasuringReceiver</c> (the 8902A tuned RF
    /// level/power). Runs over any <see cref="Visa.IInstrumentSession"/>. New interface for issue #130 (P1 #91).
    /// </summary>
    public interface IModulationAnalyzer
    {
        /// <summary>The resource string this analyzer's session was opened for.</summary>
        string ResourceName { get; }

        /// <summary>Identifies the instrument (the 8901 has no <c>*IDN?</c>; returns a descriptor).</summary>
        string Identify();

        /// <summary>Device clear + Instrument Preset (<c>IP</c>).</summary>
        void Initialize();

        /// <summary>Tunes to the carrier frequency, in MHz (auto operation).</summary>
        void TuneMHz(double megahertz);

        /// <summary>Selects a measurement, triggers it (with settling), and reads the result.</summary>
        double Measure(ModulationMeasurementType type);
    }

    /// <summary>HP 8901 measurement types (the <c>M1</c>…<c>M5</c> function codes). M1 = AM and M2 = FM are
    /// manual-confirmed; ΦM / RF-power / frequency (M3–M5) mappings are bench-confirm items.</summary>
    public enum ModulationMeasurementType
    {
        Am,                  // M1
        Fm,                  // M2
        PhaseModulation,     // M3 (bench-confirm)
        RfPower,             // M4 (bench-confirm)
        Frequency            // M5 (bench-confirm)
    }
}
