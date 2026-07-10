using System;

namespace GpibUtils.Instruments.Meters
{
    /// <summary>RF power-unit conversions used by the measuring-receiver drivers.</summary>
    public static class Rf
    {
        /// <summary>Converts power in watts to dBm.</summary>
        public static double WattsToDbm(double watts)
        {
            if (watts <= 0) return double.NegativeInfinity;
            return 10.0 * Math.Log10(watts / 1e-3);
        }

        /// <summary>Converts power in dBm to watts.</summary>
        public static double DbmToWatts(double dbm) => 1e-3 * Math.Pow(10.0, dbm / 10.0);
    }
}
