using System;

namespace GpibUtils.Instruments.Meters
{
    /// <summary>An error reported by the 8902A via its sentinel data value, or a decoded read condition.</summary>
    public sealed class Hp8902AException : Exception
    {
        public int Code { get; }

        /// <summary>
        /// True when the 8902A returned its uncalibrated indicator (a row of 'C'/'A') instead of a
        /// number — RECAL/UNCAL is set and the receiver needs a CALIBRATE at the current level.
        /// </summary>
        public bool IsUncal { get; }

        /// <summary>
        /// True when the read came back empty / had no numeric content (a transient GPIB timing glitch:
        /// the read raced Data Ready or an RF-range auto-range). It is retriable — settle + re-trigger —
        /// NOT genuinely bad data, so it should be recovered rather than failing the point.
        /// </summary>
        public bool IsEmpty { get; }

        public Hp8902AException(int code, string message) : base($"8902A Error {code}: {message}")
        {
            Code = code;
        }

        private Hp8902AException(string message, bool uncal, bool empty) : base(message)
        {
            Code = -1;
            IsUncal = uncal;
            IsEmpty = empty;
        }

        /// <summary>An uncalibrated ("CCCC") reading — the receiver needs calibration at this level.</summary>
        public static Hp8902AException Uncal() =>
            new Hp8902AException("8902A reading uncalibrated (RECAL) — needs CALIBRATE at this level", uncal: true, empty: false);

        /// <summary>An empty / no-numeric-content read — a transient timing glitch, retriable.</summary>
        public static Hp8902AException EmptyRead() =>
            new Hp8902AException("8902A empty/short read — transient (retrying)", uncal: false, empty: true);

        /// <summary>A CALIBRATE (C1) raised the instrument-error bit after it completed — sampled during
        /// the post-calibrate settle, where it was previously invisible. The specific code (often
        /// Error 35, level error during calibration) shows on the 8902A front panel.</summary>
        public static Hp8902AException CalibrateError(int statusByte) =>
            new Hp8902AException($"CALIBRATE raised an instrument error (status 0x{statusByte:X2}) — read " +
                "the 8902A front panel for the code (e.g. Error 35, level error during calibration)",
                uncal: false, empty: false);

        /// <summary>Known 8902A operating-error messages (Operation manual, p.3-286).</summary>
        public static string Describe(int code)
        {
            switch (code)
            {
                case 1: return "Input level too high";
                case 2: return "Input level too low";
                case 5: return "RF input overload";
                case 6: return "Voltmeter/display overload";
                case 15: return "Calibration factor error (load cal factors)";
                case 17: return "Tuned RF Level circuits underdriven";
                case 18: return "RF Power will not calibrate";
                case 96: return "No input signal sensed (cannot tune to a signal)";
                default: return "see 8902A manual error table";
            }
        }
    }
}
