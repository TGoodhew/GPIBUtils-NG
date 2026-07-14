namespace GpibUtils.Instruments.Meters
{
    /// <summary>
    /// An RF power meter (HP E4418B, HP 438A, …). Zeroes and calibrates its sensor, then measures absolute
    /// power in dBm — over any <see cref="Visa.IInstrumentSession"/>. Distinct from
    /// <see cref="IMeasuringReceiver"/> (the tuned 8902A), which does far more than sensor power.
    ///
    /// <para>How the sensor cal factor is applied is instrument-specific and stays on the concrete driver:
    /// the SCPI E4418B takes a carrier frequency (<c>HpE4418B.SetFrequencyMHz</c>) and looks the factor up;
    /// the older 438A takes the cal factor as a percentage (<c>Hp438A.SetCalFactorPercent</c>).</para>
    /// </summary>
    public interface IPowerMeter
    {
        /// <summary>The resource string this driver's session was opened for.</summary>
        string ResourceName { get; }

        /// <summary>Reads the instrument identity (where the instrument supports an identity query).</summary>
        string Identify();

        /// <summary>Device clear + reset to a known state.</summary>
        void Initialize();

        /// <summary>Zeroes and calibrates the sensor against the meter's reference.</summary>
        void ZeroAndCalibrate();

        /// <summary>Triggers and reads the measured power in dBm.</summary>
        double MeasurePowerDbm();
    }
}
