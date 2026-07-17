using System.Collections.Generic;

namespace GpibUtils.Instruments.Meters
{
    /// <summary>
    /// A vector voltmeter (HP 8508A): a tuned dual-channel RF receiver (100 kHz–2 GHz) that measures channel
    /// A/B voltage and power, the B/A magnitude ratio and B−A phase, transmission, group delay, SWR,
    /// reflection coefficient, admittance and impedance. Distinct from the scalar meter interfaces. Runs over
    /// any <see cref="Visa.IInstrumentSession"/>. New interface for issue #104 (HP 8508A Vector Voltmeter).
    /// </summary>
    public interface IVectorVoltmeter
    {
        /// <summary>The resource string this instrument's session was opened for.</summary>
        string ResourceName { get; }

        /// <summary>Reads the instrument identity (<c>*IDN?</c>).</summary>
        string Identify();

        /// <summary>Device clear + reset to a known state (auto frequency-band tracking on).</summary>
        void Initialize();

        /// <summary>Enables or disables automatic frequency-band selection (<c>FREQuency:BAND:AUTO</c>).</summary>
        void SetFrequencyBandAuto(bool on);

        /// <summary>Sets the averaging count — 2^count internal readings per result (<c>AVERage:COUNt</c>, 0–10).</summary>
        void SetAveragingCount(int count);

        /// <summary>Configures, triggers and reads a single measurement (<c>MEASure? &lt;meas&gt;</c>).</summary>
        double Measure(VectorMeasurement measurement);

        /// <summary>Configures, triggers and reads several measurements at once (<c>MEASure?</c> with a
        /// comma-separated list); returns one value per requested measurement.</summary>
        IReadOnlyList<double> MeasureMany(params VectorMeasurement[] measurements);
    }

    /// <summary>A vector-voltmeter measurement quantity (the HP 8508A <c>SENSe</c>/<c>MEASure?</c> mnemonics).</summary>
    public enum VectorMeasurement
    {
        ChannelAVoltage,        // AVOLtage
        ChannelBVoltage,        // BVOLtage
        ChannelAPower,          // APOWer
        ChannelBPower,          // BPOWer
        RatioBA,                // BA (B/A magnitude ratio)
        Phase,                  // PHASe (B-A phase)
        Transmission,           // TRANsmission
        GroupDelay,             // DELay
        Swr,                    // SWR
        ReflectionCoefficient,  // RHO
        Admittance,             // Y
        Impedance               // Z
    }

    /// <summary>Maps <see cref="VectorMeasurement"/> to its HP 8508A mnemonic.</summary>
    internal static class VectorMeasurementScpi
    {
        public static string Mnemonic(this VectorMeasurement m)
        {
            switch (m)
            {
                case VectorMeasurement.ChannelAVoltage: return "AVOLtage";
                case VectorMeasurement.ChannelBVoltage: return "BVOLtage";
                case VectorMeasurement.ChannelAPower: return "APOWer";
                case VectorMeasurement.ChannelBPower: return "BPOWer";
                case VectorMeasurement.RatioBA: return "BA";
                case VectorMeasurement.Phase: return "PHASe";
                case VectorMeasurement.Transmission: return "TRANsmission";
                case VectorMeasurement.GroupDelay: return "DELay";
                case VectorMeasurement.Swr: return "SWR";
                case VectorMeasurement.ReflectionCoefficient: return "RHO";
                case VectorMeasurement.Admittance: return "Y";
                case VectorMeasurement.Impedance: return "Z";
                default: throw new System.ArgumentOutOfRangeException(nameof(m), m, null);
            }
        }
    }
}
