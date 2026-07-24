namespace GpibUtils.Verification
{
    /// <summary>One point in a signal-source verification plan: a commanded CW frequency (MHz) and output
    /// power (dBm), with optional per-point tolerances (power in dB, frequency in ppm) and notes.</summary>
    public sealed class SignalSourcePoint
    {
        public double FrequencyMHz { get; set; }
        public double PowerDbm { get; set; }

        /// <summary>Per-point power tolerance in dB (|measured − set|); null falls back to the run default.</summary>
        public double? PowerToleranceDb { get; set; }

        /// <summary>Per-point frequency tolerance in ppm; null falls back to the run default.</summary>
        public double? FrequencyTolerancePpm { get; set; }

        public string Notes { get; set; }

        public override string ToString()
        {
            var s = $"{FrequencyMHz:G9} MHz @ {PowerDbm:G4} dBm";
            if (!string.IsNullOrEmpty(Notes)) s += $" ({Notes})";
            return s;
        }
    }

    /// <summary>Run-wide options for a signal-source verification.</summary>
    public sealed class SignalSourceOptions
    {
        /// <summary>Delay after setting a point (and turning RF on) before sampling, ms. Default 500.</summary>
        public int SettlingMs { get; set; } = 500;

        /// <summary>Reference reads averaged per point. Default 4.</summary>
        public int Samples { get; set; } = 4;

        /// <summary>Turn the source's RF output off when the run ends. Default true.</summary>
        public bool RfOffOnExit { get; set; } = true;

        /// <summary>Default per-point power tolerance in dB; null = report-only unless a point sets its own.</summary>
        public double? DefaultPowerToleranceDb { get; set; }

        /// <summary>Default per-point frequency tolerance in ppm; null = report-only unless a point sets its own.</summary>
        public double? DefaultFrequencyTolerancePpm { get; set; }
    }

    /// <summary>The measured result for one signal-source verification point.</summary>
    public sealed class SignalSourceResult
    {
        public int Index { get; set; }
        public double FrequencyMHz { get; set; }
        public double PowerDbm { get; set; }
        public int Samples { get; set; }
        public string Notes { get; set; }

        // ---- power (present when a power reference was used) ----
        public bool PowerMeasured { get; set; }
        public double MeasuredPowerDbm { get; set; }
        public double PowerErrorDb { get; set; }
        public double PowerStdDevDb { get; set; }
        public double? PowerToleranceDb { get; set; }

        /// <summary>"PASS"/"FAIL", or null when no power tolerance applied (report-only or not measured).</summary>
        public string PowerVerdict { get; set; }

        // ---- frequency (present when a frequency reference was used) ----
        public bool FrequencyMeasured { get; set; }
        public double MeasuredFrequencyHz { get; set; }
        public double FrequencyErrorHz { get; set; }
        public double FrequencyErrorPpm { get; set; }
        public double? FrequencyTolerancePpm { get; set; }

        /// <summary>"PASS"/"FAIL", or null when no frequency tolerance applied (report-only or not measured).</summary>
        public string FrequencyVerdict { get; set; }

        /// <summary>Non-null when this point threw mid-run (I/O error, driver timeout, unreachable reference):
        /// the exception message. No quantity was measured and both verdicts are null.</summary>
        public string Error { get; set; }

        /// <summary>The point could not be measured; counts as neither Passed nor Failed but must not be
        /// mistaken for a clean run — a run with any errored point exits non-zero.</summary>
        public bool Errored => Error != null;

        /// <summary>True only if no measured quantity failed and at least one graded quantity passed.</summary>
        public bool Passed =>
            !Errored && PowerVerdict != "FAIL" && FrequencyVerdict != "FAIL" &&
            (PowerVerdict == "PASS" || FrequencyVerdict == "PASS");

        /// <summary>True if any graded quantity failed its tolerance.</summary>
        public bool Failed => PowerVerdict == "FAIL" || FrequencyVerdict == "FAIL";
    }
}
