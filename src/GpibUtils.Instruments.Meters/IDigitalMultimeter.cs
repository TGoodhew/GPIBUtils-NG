using System.Collections.Generic;

namespace GpibUtils.Instruments.Meters
{
    /// <summary>Trigger source for a DMM measurement (34401A <c>TRIGger:SOURce</c>).</summary>
    public enum TriggerSource
    {
        /// <summary>Immediate (continuous) — the default; a measurement is taken as soon as it is triggered.</summary>
        Immediate,

        /// <summary>Bus — the measurement waits for a <c>*TRG</c> (group execute trigger) over GPIB.</summary>
        Bus,

        /// <summary>External — the measurement waits for a rising edge on the rear-panel Ext Trig input.</summary>
        External
    }

    /// <summary>Autozero mode (34401A <c>SENSe:ZERO:AUTO</c>).</summary>
    public enum AutoZeroMode
    {
        /// <summary>Off — no automatic internal zero (faster; drifts with temperature).</summary>
        Off,

        /// <summary>Once — take one internal zero now, then hold it (OFF thereafter).</summary>
        Once,

        /// <summary>On — take an internal zero before every measurement (most accurate; slower).</summary>
        On
    }

    /// <summary>
    /// A programmable digital multimeter (HP/Agilent/Keysight 34401A). Exposes the SCPI measurement
    /// surface — configure a function, tune sense/trigger/sample settings, then read one or many values —
    /// over any <see cref="Visa.IInstrumentSession"/>, so the same driver runs on NI-VISA, a
    /// Prologix/AR488 adapter, or the in-memory simulator.
    /// </summary>
    public interface IDigitalMultimeter
    {
        /// <summary>The resource string this driver's session was opened for.</summary>
        string ResourceName { get; }

        /// <summary>Reads the instrument identity (<c>*IDN?</c>).</summary>
        string Identify();

        /// <summary>Device clear + <c>*RST</c> + <c>*CLS</c> — a clean, known power-on state.</summary>
        void Initialize();

        /// <summary>Instrument reset (<c>*RST</c>).</summary>
        void Reset();

        /// <summary>
        /// Configures a measurement function (<c>CONFigure</c>), optionally with a range and resolution.
        /// A null/empty <paramref name="range"/> or <paramref name="resolution"/> leaves that argument at
        /// the instrument default (autorange / default resolution). Accepts numeric values or
        /// <c>MIN</c>/<c>MAX</c>/<c>DEF</c>.
        /// </summary>
        void Configure(MeasurementFunction function, string range = null, string resolution = null);

        /// <summary>Queries the present measurement configuration (<c>CONFigure?</c>).</summary>
        string QueryConfiguration();

        /// <summary>Queries the present measurement function (<c>SENSe:FUNCtion?</c>).</summary>
        string QueryFunction();

        /// <summary>Triggers and reads a single value (<c>READ?</c> — INITiate + FETCh).</summary>
        double ReadValue();

        /// <summary>
        /// Triggers a burst of <paramref name="count"/> readings (sets the sample count, then <c>READ?</c>)
        /// and returns them all. For large counts, raise the session timeout — <c>READ?</c> blocks until
        /// every sample has been taken.
        /// </summary>
        double[] ReadValues(int count);

        /// <summary>Fetches the readings already taken since the last <see cref="Initiate"/> (<c>FETCh?</c>).</summary>
        double[] Fetch();

        /// <summary>Arms the trigger system for a measurement without reading (<c>INITiate</c>).</summary>
        void Initiate();

        /// <summary>Sends a bus (group execute) trigger (<c>*TRG</c>); use with <see cref="TriggerSource.Bus"/>.</summary>
        void BusTrigger();

        /// <summary>Sets the trigger source (<c>TRIGger:SOURce</c>).</summary>
        void SetTriggerSource(TriggerSource source);

        /// <summary>Sets the trigger count — how many trigger cycles to accept (<c>TRIGger:COUNt</c>).</summary>
        void SetTriggerCount(int count);

        /// <summary>Sets the sample count — readings taken per trigger (<c>SAMPle:COUNt</c>).</summary>
        void SetSampleCount(int count);

        /// <summary>Sets the integration time in power-line cycles for a rangeable function (<c>NPLC</c>).</summary>
        void SetNplc(MeasurementFunction function, double nplc);

        /// <summary>Enables (<c>ON</c>) or disables (<c>OFF</c>) autorange for a rangeable function.</summary>
        void SetAutoRange(MeasurementFunction function, bool on);

        /// <summary>Reads the next entry from the error queue (<c>SYSTem:ERRor?</c>), or <c>+0,"No error"</c>.</summary>
        string NextError();

        /// <summary>Drains and returns the error queue until <c>+0,"No error"</c> (capped to avoid a runaway).</summary>
        IReadOnlyList<string> DrainErrors();

        /// <summary>Runs the internal self-test (<c>*TST?</c>); true = passed.</summary>
        bool SelfTest();
    }
}
