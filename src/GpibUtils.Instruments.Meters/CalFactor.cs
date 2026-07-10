using System.Collections.Generic;

namespace GpibUtils.Instruments.Meters
{
    /// <summary>A power-sensor / converter calibration factor at one frequency.</summary>
    public sealed class CalFactor
    {
        public double FreqMHz { get; }
        public double Cf { get; }   // percent, e.g. 96.3

        public CalFactor(double freqMHz, double cf)
        {
            FreqMHz = freqMHz;
            Cf = cf;
        }
    }

    /// <summary>
    /// A cal-factor table from a sensor/converter rear label (REF CF @ 50 MHz = 100%), supplied by the
    /// bench owner. Loaded into the 8902A's Frequency-Offset RF-Power table. The <see cref="Default"/>
    /// table is the reference bench's sensor and is a convenience default for the CLI / tests — replace
    /// it with the values off your own sensor's label for accurate power.
    /// </summary>
    public static class ConverterCalFactors
    {
        public const double ReferenceCf = 100.0; // REF CF (50 MHz)

        /// <summary>Sensor S/N 2407A00808, OPT 001 — 2 to 18 GHz.</summary>
        public static IReadOnlyList<CalFactor> Default { get; } = new[]
        {
            new CalFactor(2000, 96.3),  new CalFactor(3000, 94.8),  new CalFactor(4000, 93.9),
            new CalFactor(5000, 92.9),  new CalFactor(6000, 91.9),  new CalFactor(7000, 91.1),
            new CalFactor(8000, 90.3),  new CalFactor(9000, 89.3),  new CalFactor(10000, 88.5),
            new CalFactor(11000, 87.5), new CalFactor(12400, 87.0), new CalFactor(13000, 86.1),
            new CalFactor(14000, 85.6), new CalFactor(15000, 85.4), new CalFactor(16000, 84.9),
            new CalFactor(17000, 84.6), new CalFactor(18000, 84.1),
        };
    }
}
