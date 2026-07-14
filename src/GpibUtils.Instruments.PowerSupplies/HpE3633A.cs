using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.PowerSupplies
{
    /// <summary>
    /// Driver for the HP/Agilent E3633A single-output DC power supply (0–8 V/20 A or 0–20 V/10 A range) —
    /// a plain SCPI instrument. Sets output voltage / current limit, gates the output, and reads back the
    /// measured voltage and current. Ported from <c>E3633A-Demo</c> (issue #19). Runs over any
    /// <see cref="IInstrumentSession"/>.
    /// </summary>
    public sealed class HpE3633A : IDcPowerSupply
    {
        /// <summary>GPIB address of the E3633A — its documented factory-default GPIB address is 5 (E3633A
        /// User's Guide: "The address is set to '05' when the power supply is shipped from the factory").
        /// Override with <c>--address</c>. Note: the legacy <c>E3633A-Demo</c> source hardcoded
        /// <c>GPIB0::27::INSTR</c> (a bench value) — configure the bench's real address via
        /// <c>config address set hpe3633a …</c> rather than relying on this fallback.</summary>
        public const string DefaultResource = "GPIB0::5::INSTR";

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public HpE3633A(IInstrumentSession session) =>
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

        public string Identify() => Query("*IDN?");

        public void Initialize()
        {
            _session.Clear();   // GPIB device clear
            Send("*RST");       // reset to the power-on state (output off)
            Send("*CLS");       // clear status registers + error queue
        }

        public void Reset() => Send("*RST");

        public void SetVoltage(double volts) =>
            Send("VOLT " + volts.ToString("0.###", CultureInfo.InvariantCulture));

        public void SetCurrentLimit(double amps) =>
            Send("CURR " + amps.ToString("0.###", CultureInfo.InvariantCulture));

        public void SetOutput(bool on) => Send(on ? "OUTP ON" : "OUTP OFF");

        public double MeasureVoltage() => ParseReading(Query("MEAS:VOLT?"));

        public double MeasureCurrent() => ParseReading(Query("MEAS:CURR?"));

        /// <summary>Sets the over-voltage protection trip level (volts) — <c>VOLTage:PROTection</c>.</summary>
        public void SetOverVoltageProtection(double volts) =>
            Send("VOLT:PROT " + volts.ToString("0.###", CultureInfo.InvariantCulture));

        /// <summary>Enables/disables over-voltage protection (<c>VOLTage:PROTection:STATe</c>).</summary>
        public void SetOverVoltageProtectionEnabled(bool on) => Send($"VOLT:PROT:STAT {(on ? "ON" : "OFF")}");

        /// <summary>Reads the next entry from the error queue (<c>SYSTem:ERRor?</c>).</summary>
        public string NextError() => Query("SYST:ERR?");

        /// <summary>Parses a SCPI numeric reading (volts / amps) to a double.</summary>
        internal static double ParseReading(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                throw new FormatException("Empty E3633A reading.");
            if (!double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                throw new FormatException($"Unrecognized E3633A reading: '{raw}'.");
            return v;
        }
    }
}
