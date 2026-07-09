namespace GpibUtils.Instruments.SignalSources
{
    /// <summary>
    /// A CW signal source / sweep generator (e.g. HP 8340B, HP 8673B). The common control surface the
    /// drivers in this category expose, so callers and future front-ends can treat any source uniformly.
    /// </summary>
    public interface ISignalSource
    {
        /// <summary>The resource string this source's session was opened for.</summary>
        string ResourceName { get; }

        /// <summary>Device clear + preset to a known state (no stale errors/SRQ), RF off.</summary>
        void Initialize();

        /// <summary>Instrument preset to its default state.</summary>
        void Preset();

        /// <summary>Sets the CW output frequency, in MHz.</summary>
        void SetFrequencyMHz(double mhz);

        /// <summary>Sets the output power, in dBm.</summary>
        void SetPowerDbm(double dbm);

        /// <summary>Enables the RF output.</summary>
        void RfOn();

        /// <summary>Disables the RF output.</summary>
        void RfOff();
    }
}
