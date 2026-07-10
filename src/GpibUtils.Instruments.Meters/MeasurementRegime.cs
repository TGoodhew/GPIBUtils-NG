namespace GpibUtils.Instruments.Meters
{
    /// <summary>How an RF frequency is presented to the measuring receiver.</summary>
    public enum MeasurementRegime
    {
        /// <summary>Measured directly on the 8902A (no converter, no LO).</summary>
        Direct,

        /// <summary>Measured through a microwave converter (e.g. 11793A) with an external LO
        /// (the 8902A's Frequency-Offset mode).</summary>
        Converted
    }
}
