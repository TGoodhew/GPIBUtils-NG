using GpibUtils.Instruments.Meters;

namespace GpibUtils.Verification
{
    /// <summary>One point in a 5440 verification plan: a nominal output voltage, optional DMM range,
    /// optional tolerance (ppm), and free-text notes.</summary>
    public sealed class VerificationPoint
    {
        public double NominalVolts { get; set; }

        /// <summary>DMM DCV range for this point ("AUTO" or a full-scale voltage); null uses the global range.</summary>
        public string Range { get; set; }

        /// <summary>Per-point tolerance in ppm; null falls back to the run default (report-only if that is also null).</summary>
        public double? TolerancePpm { get; set; }

        public string Notes { get; set; }

        public override string ToString()
        {
            var s = $"nominal={NominalVolts:G7} V";
            if (Range != null) s += $" range={Range}";
            if (TolerancePpm.HasValue) s += $" tol={TolerancePpm.Value:0.##} ppm";
            if (!string.IsNullOrEmpty(Notes)) s += $" ({Notes})";
            return s;
        }
    }

    /// <summary>The measured result for one verification point.</summary>
    public sealed class VerificationResult
    {
        public int Index { get; set; }
        public double NominalVolts { get; set; }
        public string Range { get; set; }
        public double MeasuredVolts { get; set; }
        public double AbsErrorVolts { get; set; }

        /// <summary>Error in ppm of reading; NaN at a 0 V nominal (ppm-of-reading is undefined there).</summary>
        public double PpmOfReading { get; set; }

        public double StdDevVolts { get; set; }
        public int Samples { get; set; }
        public double? TolerancePpm { get; set; }

        /// <summary>"PASS" / "FAIL" / "ERROR", or null when no tolerance applied (report-only). "ERROR"
        /// marks a point that could not be measured (see <see cref="Error"/>).</summary>
        public string Verdict { get; set; }

        /// <summary>Non-null when this point threw mid-run (I/O error, driver timeout, unreachable DMM): the
        /// exception message. The measured fields are then NaN and <see cref="Verdict"/> is "ERROR".</summary>
        public string Error { get; set; }

        public string Notes { get; set; }

        public bool Passed => Verdict == "PASS";
        public bool Failed => Verdict == "FAIL";

        /// <summary>The point could not be measured; counts as neither Passed nor Failed but must not be
        /// mistaken for a clean run — a run with any errored point exits non-zero.</summary>
        public bool Errored => Verdict == "ERROR";
    }

    /// <summary>Run-wide configuration for a 5440 verification.</summary>
    public sealed class VerificationOptions
    {
        /// <summary>Global DMM DCV range ("AUTO" or a full-scale voltage); a point's own range overrides it.</summary>
        public string GlobalRange { get; set; } = "AUTO";

        /// <summary>DMM integration time (NPLC). Default 10.</summary>
        public double Nplc { get; set; } = 10.0;

        /// <summary>DMM autozero mode. Default On.</summary>
        public AutoZeroMode Autozero { get; set; } = AutoZeroMode.On;

        /// <summary>Enable the DMM's &gt;10 GΩ input on the low DCV ranges (INP:IMP:AUTO ON).</summary>
        public bool HighImpedance { get; set; }

        /// <summary>External 4-wire sense on the 5440 (ESNS) vs internal 2-wire (ISNS). Default external.</summary>
        public bool SenseExternal { get; set; } = true;

        /// <summary>Delay after OPER before sampling, ms. Default 1000.</summary>
        public int SettlingMs { get; set; } = 1000;

        /// <summary>DMM reads averaged per point. Default 4.</summary>
        public int Samples { get; set; } = 4;

        /// <summary>Default per-point tolerance in ppm; null = report-only unless a point sets its own.</summary>
        public double? DefaultTolerancePpm { get; set; }

        /// <summary>Return the 5440 to Standby when the run finishes. Default true.</summary>
        public bool StandbyOnExit { get; set; } = true;
    }
}
