namespace GpibUtils.Instruments.Meters
{
    /// <summary>
    /// An RF power meter (HP E4418B, HP 438A, …). Zeroes and calibrates its sensor, is told the carrier
    /// frequency for the cal-factor correction, then measures absolute power in dBm — over any
    /// <see cref="Visa.IInstrumentSession"/>. Distinct from <see cref="IMeasuringReceiver"/> (the tuned
    /// 8902A), which does far more than sensor power.
    /// </summary>
    public interface IPowerMeter
    {
        /// <summary>The resource string this driver's session was opened for.</summary>
        string ResourceName { get; }

        /// <summary>Reads the instrument identity (<c>*IDN?</c> where supported).</summary>
        string Identify();

        /// <summary>Device clear + reset to a known state.</summary>
        void Initialize();

        /// <summary>Zeroes and calibrates the sensor against the meter's reference.</summary>
        void ZeroAndCalibrate();

        /// <summary>Sets the carrier frequency (MHz) used to apply the sensor cal factor.</summary>
        void SetFrequencyMHz(double mhz);

        /// <summary>Triggers and reads the measured power in dBm.</summary>
        double MeasurePowerDbm();
    }
}
