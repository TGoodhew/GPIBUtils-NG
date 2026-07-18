using System;

namespace GpibUtils.Instruments.Meters
{
    /// <summary>
    /// A measurement function of a digital multimeter (HP 34401A). The enum values map to the SCPI
    /// function keywords used by <c>CONFigure</c> / <c>SENSe:FUNCtion</c>; see <see cref="MeasurementFunctionScpi.Root"/>.
    /// </summary>
    public enum MeasurementFunction
    {
        /// <summary>DC voltage — SCPI <c>VOLT:DC</c>.</summary>
        DcVoltage,

        /// <summary>AC (true-RMS) voltage — SCPI <c>VOLT:AC</c>.</summary>
        AcVoltage,

        /// <summary>DC current — SCPI <c>CURR:DC</c>.</summary>
        DcCurrent,

        /// <summary>AC (true-RMS) current — SCPI <c>CURR:AC</c>.</summary>
        AcCurrent,

        /// <summary>2-wire resistance — SCPI <c>RES</c>.</summary>
        Resistance2Wire,

        /// <summary>4-wire resistance — SCPI <c>FRES</c>.</summary>
        Resistance4Wire,

        /// <summary>Frequency — SCPI <c>FREQ</c>.</summary>
        Frequency,

        /// <summary>Period — SCPI <c>PER</c>.</summary>
        Period,

        /// <summary>Continuity — SCPI <c>CONT</c> (no range/resolution arguments).</summary>
        Continuity,

        /// <summary>Diode test — SCPI <c>DIOD</c> (no range/resolution arguments).</summary>
        Diode
    }

    /// <summary>SCPI keyword mapping for <see cref="MeasurementFunction"/>.</summary>
    public static class MeasurementFunctionScpi
    {
        /// <summary>
        /// The SCPI function root used with <c>CONF:</c> and <c>SENSe:</c> (e.g. <c>VOLT:DC</c>). Continuity
        /// and diode have their own bare <c>CONF:CONT</c> / <c>CONF:DIOD</c> forms and take no root argument.
        /// </summary>
        public static string Root(this MeasurementFunction function)
        {
            switch (function)
            {
                case MeasurementFunction.DcVoltage: return "VOLT:DC";
                case MeasurementFunction.AcVoltage: return "VOLT:AC";
                case MeasurementFunction.DcCurrent: return "CURR:DC";
                case MeasurementFunction.AcCurrent: return "CURR:AC";
                case MeasurementFunction.Resistance2Wire: return "RES";
                case MeasurementFunction.Resistance4Wire: return "FRES";
                case MeasurementFunction.Frequency: return "FREQ";
                case MeasurementFunction.Period: return "PER";
                case MeasurementFunction.Continuity: return "CONT";
                case MeasurementFunction.Diode: return "DIOD";
                default: throw new ArgumentOutOfRangeException(nameof(function), function, null);
            }
        }

        /// <summary>True for functions that accept a range/resolution argument on <c>CONFigure</c> and support
        /// <c>NPLC</c> / manual ranging (everything except continuity and diode).</summary>
        public static bool IsRangeable(this MeasurementFunction function) =>
            function != MeasurementFunction.Continuity && function != MeasurementFunction.Diode;
    }
}
