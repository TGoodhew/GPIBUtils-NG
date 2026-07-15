using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.Calibrators
{
    /// <summary>Boost mode of the 5440 output stage.</summary>
    public enum Fluke5440BoostMode
    {
        /// <summary>Boost off — internal amplifier / normal voltage mode (<c>BSTO</c>).</summary>
        Off,

        /// <summary>Voltage boost — drive an external voltage-boost amplifier (<c>BSTV</c>).</summary>
        Voltage,

        /// <summary>Current boost — drive an external current-boost amplifier (<c>BSTC</c>).</summary>
        Current
    }

    /// <summary>
    /// Driver for the Fluke 5440A / 5440B DC Voltage Calibrator — a pre-IEEE-488.2 <b>mnemonic</b> HP-IB
    /// instrument (0–1100 V). Programs the output level, switches Operate/Standby, selects sensing/guard,
    /// sets voltage/current limits, and exposes the status/error and self-test surface. Ported from the
    /// <c>5440Controller</c> Spectre console app (issue #35); the <c>34401AController</c> app carried only a
    /// minimal 5440 subset — this is the full driver. Runs over any <see cref="IInstrumentSession"/>.
    ///
    /// <para><b>Identity:</b> the 5440 has no <c>*IDN?</c>; the only ID over the bus is the firmware version
    /// (<c>GVRS</c>, a numeric mantissa like "02.01"). Model and serial number are not retrievable over GPIB.</para>
    ///
    /// <para><b>Numeric format:</b> the 5440 manual specifies &lt; 8 significant digits for a value; outgoing
    /// numbers are formatted <c>G7</c> (invariant culture) to stay inside that bound.</para>
    ///
    /// <para><b>Completion:</b> the legacy app used no SRQ — after <see cref="SetOutputState"/> Operate the
    /// output settles (~1 s for a range change); poll <see cref="GetDoingState"/> (<c>GONG</c>, 0 = idle) to
    /// confirm the calibrator has finished a self-test or settle. A completion handshake through the #43 SRQ
    /// engine (<c>SSRQ</c>/<c>GSPB</c>) is a possible future refinement, deferred until bench-confirmed.</para>
    /// </summary>
    public sealed class Fluke5440A : IDcVoltageCalibrator
    {
        /// <summary>GPIB address of the 5440 — its documented factory-default HP-IB address is 7 (confirmed
        /// against the "5440B-AF User Manual"; also the legacy app's default). Override with <c>--address</c>.
        /// As always on this bench, never trust bus-scan discovery — the HP-IB extenders make every address
        /// look present.</summary>
        public const string DefaultResource = "GPIB0::7::INSTR";

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public Fluke5440A(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;

        /// <summary>Every command sent through the driver, in order (for CLI echo / tests).</summary>
        public IReadOnlyList<string> History => _history;

        private void Send(string command)
        {
            _session.Write(command);
            _history.Add(command);
        }

        private string Query(string command)
        {
            _history.Add(command);
            return (_session.Query(command) ?? string.Empty).Trim();
        }

        /// <summary>Formats a value the way the 5440 expects — <c>G7</c>, invariant, &lt; 8 significant digits.</summary>
        internal static string Format(double value) => value.ToString("G7", CultureInfo.InvariantCulture);

        // ---- identity -------------------------------------------------------

        /// <summary>The 5440 predates <c>*IDN?</c>; returns a fixed descriptor.</summary>
        public string Identify() => "FLUKE 5440A/5440B DC Voltage Calibrator (no *IDN?; use GVRS for firmware)";

        public string FirmwareVersion() => Query("GVRS");

        // ---- state ----------------------------------------------------------

        public void Initialize()
        {
            _session.Clear();   // HP-IB device clear — drop pending I/O
            Send("RESET");      // return to power-on state (standby, output cleared)
        }

        public void Reset() => Send("RESET");

        // ---- output ---------------------------------------------------------

        public void SetOutputVolts(double volts) => Send("SOUT " + Format(volts));

        public double GetOutputVolts() => ParseValue(Query("GOUT"), "GOUT");

        /// <summary>Adds a delta to the present output level (<c>INCR &lt;delta&gt;</c>).</summary>
        public void IncrementOutput(double delta) => Send("INCR " + Format(delta));

        /// <summary>Stores the present output as the reference (<c>SREF</c>).</summary>
        public void StoreReference() => Send("SREF");

        /// <summary>Returns the output to the stored reference (<c>GREF</c>).</summary>
        public void GoToReference() => Send("GREF");

        public void SetOutputState(CalibratorOutputState state) =>
            Send(state == CalibratorOutputState.Operate ? "OPER" : "STBY");

        /// <summary>Connects the programmed output to the terminals (<c>OPER</c>).</summary>
        public void Operate() => SetOutputState(CalibratorOutputState.Operate);

        /// <summary>Disconnects the output from the terminals (<c>STBY</c>).</summary>
        public void Standby() => SetOutputState(CalibratorOutputState.Standby);

        // ---- mode / sense ---------------------------------------------------

        public void SetSenseMode(CalibratorSenseMode mode) =>
            Send(mode == CalibratorSenseMode.ExternalFourWire ? "ESNS" : "ISNS");

        /// <summary>Selects external (<c>EGRD</c>) or internal (<c>IGRD</c>) guard.</summary>
        public void SetExternalGuard(bool external) => Send(external ? "EGRD" : "IGRD");

        /// <summary>Turns the output divider on (<c>DIVY</c>) or off (<c>DIVN</c>).</summary>
        public void SetDivider(bool on) => Send(on ? "DIVY" : "DIVN");

        /// <summary>Selects the boost mode (<c>BSTV</c>/<c>BSTC</c>/<c>BSTO</c>).</summary>
        public void SetBoostMode(Fluke5440BoostMode mode)
        {
            switch (mode)
            {
                case Fluke5440BoostMode.Voltage: Send("BSTV"); break;
                case Fluke5440BoostMode.Current: Send("BSTC"); break;
                case Fluke5440BoostMode.Off: Send("BSTO"); break;
                default: throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        // ---- limits ---------------------------------------------------------

        /// <summary>Sets the output voltage limit (<c>SVLM &lt;v&gt;</c>; sign selects +/-).</summary>
        public void SetVoltageLimit(double volts) => Send("SVLM " + Format(volts));

        /// <summary>Reads the voltage limits (<c>GVLM</c>), raw (the 5440 returns a bounded pair).</summary>
        public string GetVoltageLimits() => Query("GVLM");

        /// <summary>Sets the output current limit (<c>SCLM &lt;a&gt;</c>).</summary>
        public void SetCurrentLimit(double amps) => Send("SCLM " + Format(amps));

        /// <summary>Reads the current limits (<c>GCLM</c>), raw.</summary>
        public string GetCurrentLimits() => Query("GCLM");

        // ---- status / errors ------------------------------------------------

        /// <summary>Reads the bit-encoded setup status (<c>GSTS</c>; decode per User Manual Table 4-5).</summary>
        public string GetStatus() => Query("GSTS");

        /// <summary>Reads and clears the error register (<c>GERR</c>). 0 = no error.</summary>
        public int GetError() => ParseInt(Query("GERR"), "GERR");

        /// <summary>Reads the "doing" state (<c>GONG</c>). 0 = idle; non-zero = a statement is executing
        /// (self-test, settle, cal). Poll to wait for completion.</summary>
        public int GetDoingState() => ParseInt(Query("GONG"), "GONG");

        /// <summary>Reads the serial-poll byte via the calibrator's own query (<c>GSPB</c>).</summary>
        public int GetSerialPollByte() => ParseInt(Query("GSPB"), "GSPB");

        /// <summary>Sets the SRQ mask (<c>SSRQ &lt;n&gt;</c>; sum of the condition codes to enable).</summary>
        public void SetSrqMask(int mask) => Send("SSRQ " + mask.ToString(CultureInfo.InvariantCulture));

        /// <summary>Reads the SRQ mask (<c>GSRQ</c>).</summary>
        public int GetSrqMask() => ParseInt(Query("GSRQ"), "GSRQ");

        // ---- self-test ------------------------------------------------------

        /// <summary>Runs the analog self-test (<c>TSTA</c>); poll <see cref="GetDoingState"/> for idle.</summary>
        public void SelfTestAnalog() => Send("TSTA");

        /// <summary>Runs the digital self-test (<c>TSTD</c>).</summary>
        public void SelfTestDigital() => Send("TSTD");

        /// <summary>Runs the high-voltage self-test (<c>TSTH</c>). WARNING: produces dangerous voltages on the
        /// OUTPUT/SENSE HI terminals — do not touch the output while running.</summary>
        public void SelfTestHighVoltage() => Send("TSTH");

        // ---- parsing --------------------------------------------------------

        internal static double ParseValue(string raw, string mnemonic)
        {
            if (string.IsNullOrWhiteSpace(raw))
                throw new FormatException($"Empty 5440 {mnemonic} response.");
            if (!double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                throw new FormatException($"Unrecognized 5440 {mnemonic} response: '{raw}'.");
            return v;
        }

        internal static int ParseInt(string raw, string mnemonic)
        {
            if (string.IsNullOrWhiteSpace(raw))
                throw new FormatException($"Empty 5440 {mnemonic} response.");
            var s = raw.Trim();
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                return i;
            // Some firmware returns the value with a decimal mantissa; accept and truncate.
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                return (int)d;
            throw new FormatException($"Unrecognized 5440 {mnemonic} response: '{raw}'.");
        }
    }
}
