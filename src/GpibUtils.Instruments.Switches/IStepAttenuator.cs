using System.Collections.Generic;

namespace GpibUtils.Instruments.Switches
{
    /// <summary>
    /// A programmable step attenuator (HP 11713A driving an attenuator bank). Sets total attenuation in dB
    /// and can engage specific relay sections directly, over any <see cref="Visa.IInstrumentSession"/>. This
    /// is the control surface the attenuation-measurement engine (issue #34) drives.
    /// </summary>
    public interface IStepAttenuator
    {
        /// <summary>The resource string this attenuator's session was opened for.</summary>
        string ResourceName { get; }

        /// <summary>The attenuator bank configuration (section dB weights) this driver was built with.</summary>
        AttenuatorConfig Config { get; }

        /// <summary>Device clear + set to a known (0 dB) state.</summary>
        void Initialize();

        /// <summary>Sets total attenuation in dB; returns the data string sent.</summary>
        string SetAttenuationDb(int db);

        /// <summary>
        /// Engages exactly the given section digits (1–8) and bypasses all others. Config-independent relay
        /// control, used by attenuator identification. Returns the data string sent.
        /// </summary>
        string SetEngaged(IEnumerable<int> digits);
    }
}
