using System.Collections.Generic;

namespace GpibUtils.Instruments.Meters
{
    /// <summary>
    /// A tuned measuring receiver (e.g. HP 8902A) measuring attenuation as a relative (dB) Tuned RF
    /// Level, optionally via a converter. The workflow per frequency is: Begin → (Calibrate range while
    /// stepping down) → SetReference at 0 dB → read each step as dB relative. See the 8902A attenuation
    /// procedure. Runs over any <see cref="Visa.IInstrumentSession"/>.
    /// </summary>
    public interface IMeasuringReceiver
    {
        string ResourceName { get; }

        /// <summary>Device clear + preset to a known state (no stale errors/SRQ).</summary>
        void Initialize();

        /// <summary>Instrument preset to a known state.</summary>
        void Reset();

        /// <summary>Selects the RF Power (power-sensor) measurement.</summary>
        void SelectRfPower();

        /// <summary>
        /// Loads BOTH RF-Power cal-factor tables (Normal + Frequency-Offset) in a single
        /// pass. Must be called exactly once — it clears all cal-factor storage first.
        /// </summary>
        void LoadCalFactors(double referenceCf, IReadOnlyList<CalFactor> table);

        /// <summary>Zeroes the power sensor (calibrator off). Returns the zeroed reading in watts.</summary>
        double ZeroSensor();

        /// <summary>
        /// Calibrates the sensor against the 50 MHz / 1 mW reference (C1 → settle → SC → C0).
        /// Returns the reference power read back in watts (≈ 1e-3 W = 0 dBm).
        /// </summary>
        double CalibrateSensor();

        /// <summary>
        /// Begins a Tuned RF Level relative measurement at the given RF frequency, in the direct or
        /// converter (frequency-offset, with LO) regime, using the given IF <paramref name="detector"/>
        /// (Average by default; Synchronous for the deep sweep). When <paramref name="trackMode"/> is set,
        /// the receiver uses Track Mode (8902A SF 32.9) to hold lock on a drifting converted signal;
        /// Track Mode implies the Average detector and supersedes <paramref name="detector"/>.
        /// <paramref name="tuning"/> selects manual (default) or automatic signal acquisition.
        /// </summary>
        void BeginAttenuationMeasurement(double rfMHz, MeasurementRegime regime, double loMHz,
            TrflDetector detector = TrflDetector.Average, bool trackMode = false,
            TrflTuning tuning = TrflTuning.Manual);

        /// <summary>
        /// Begins an absolute RF Power measurement at the given RF frequency, in the direct or converter
        /// (frequency-offset, with LO) regime. The RF-Power cal-factor table must already be loaded for
        /// accuracy on the converter path.
        /// </summary>
        void BeginRfPowerMeasurement(double rfMHz, MeasurementRegime regime, double loMHz);

        /// <summary>
        /// Triggers a settled RF Power measurement and returns the absolute power in dBm.
        /// Throws <see cref="Hp8902AException"/> on an instrument error.
        /// </summary>
        double ReadRfPowerDbm();

        /// <summary>
        /// Prepares for the range-calibration pass: enables the RECAL/UNCAL status condition (so
        /// <see cref="RecalRequested"/> can poll it) and puts the receiver in free-run so that state
        /// tracks the live level as the attenuator steps down.
        /// </summary>
        void BeginRangeCalibration();

        /// <summary>
        /// Enables just the RECAL/UNCAL status condition (so <see cref="RecalRequested"/> can poll it)
        /// WITHOUT forcing free-run. Used on the Track-Mode path, where free-run would auto-range the
        /// receiver and shift the relative reference by a whole RF range.
        /// </summary>
        void EnableRecalStatus();

        /// <summary>
        /// True if the receiver is asking for a range calibration (8902A RECAL/UNCAL). Read by a serial
        /// poll; only CALIBRATE when this is set, per the 8902A procedure — calibrating a range that
        /// doesn't need it raises Error 35.
        /// </summary>
        bool RecalRequested();

        /// <summary>Raw serial-poll status byte, for diagnostics. Returns -1 if not readable.</summary>
        int PollStatusByte();

        /// <summary>Performs one range-calibration step (CALIBRATE) at the current level.</summary>
        void Calibrate();

        /// <summary>Clears a displayed error/condition on the instrument (8902A CL key).</summary>
        void ClearError();

        /// <summary>
        /// Forces a retune of the Tuned-RF-Level VCO to recapture the signal after the receiver has lost
        /// lock (8902A Error 96) — the manual's remedy (O&amp;C 3-116, "Blue Key, CLEAR" = HP-IB code
        /// <c>BC</c>), which recaptures the signal provided it has not drifted more than 5 MHz. Unlike
        /// <see cref="ClearError"/> (CL), which only clears the displayed error without re-acquiring lock.
        /// </summary>
        void RetuneToSignal();

        /// <summary>
        /// Releases the GPIB bus after a hung / timed-out measurement by issuing a device clear (SDC).
        /// The 8902A inhibits the bus handshake until a triggered measurement cycle completes (O&amp;C
        /// 3-22); a device clear aborts the cycle and frees the bus — it also resets the receiver to its
        /// preset state, so the caller must re-establish the measurement before using it again.
        /// </summary>
        void ReleaseBus();

        /// <summary>Sets the 0 dB reference (SET REF) at the current level.</summary>
        void SetReference();

        /// <summary>
        /// Triggers a settled measurement and returns the level in dB relative to the reference (≤ 0).
        /// Throws <see cref="Hp8902AException"/> on an instrument error.
        /// </summary>
        double ReadRelativeDb();

        /// <summary>
        /// Triggers a settled Tuned RF Level measurement and returns the <b>absolute</b> level in dBm —
        /// the level BEFORE <see cref="SetReference"/> is taken (S4/LG shows absolute dBm until SET REF
        /// re-zeroes it to relative dB). Throws <see cref="Hp8902AException"/> on an instrument error.
        /// </summary>
        double ReadTunedLevelDbm();

        /// <summary>
        /// Measures the input signal frequency (MHz) — used as a signal-presence check.
        /// Throws <see cref="Hp8902AException"/> (code 96) when no signal is sensed.
        /// </summary>
        double ReadSignalFrequencyMHz();
    }
}
