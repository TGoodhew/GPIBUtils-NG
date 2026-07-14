using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.PowerSupplies
{
    /// <summary>
    /// Driver for the Rigol DP832 triple-output programmable DC power supply — a plain SCPI instrument
    /// (CH1/CH2 = 30 V, CH3 = 5 V; all 3 A). Sets per-channel voltage / current limit, gates each output,
    /// reads back measured V/I/P, and configures OVP/OCP. Ported from the <c>DP832</c> repo (issue #15).
    /// Implements <see cref="IDcPowerSupply"/> against the <see cref="SelectedChannel"/> (default CH1), and
    /// exposes channel-explicit overloads for the other outputs. Runs over any <see cref="IInstrumentSession"/>.
    /// </summary>
    public sealed class RigolDp832 : IDcPowerSupply
    {
        /// <summary>GPIB address of the DP832 — its documented default GPIB address is 2 (DP832 User's Guide,
        /// "To Set the GPIB Address": "The default is 2"). Override with <c>--address</c>. Note: the legacy
        /// <c>DP832</c> standalone app hardcoded <c>GPIB0::1::INSTR</c>; the DP832 also speaks LXI/USB.</summary>
        public const string DefaultResource = "GPIB0::2::INSTR";

        /// <summary>Number of outputs.</summary>
        public const int ChannelCount = 3;

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public RigolDp832(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;

        /// <summary>Every command sent through the driver, in order (for CLI echo / tests).</summary>
        public IReadOnlyList<string> History => _history;

        /// <summary>The channel (1–3) the <see cref="IDcPowerSupply"/> members act on. Default CH1.</summary>
        public int SelectedChannel { get; set; } = 1;

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

        private static int Check(int channel)
        {
            if (channel < 1 || channel > ChannelCount)
                throw new ArgumentOutOfRangeException(nameof(channel), channel, $"Channel must be 1–{ChannelCount}.");
            return channel;
        }

        private static string F3(double v) => v.ToString("F3", CultureInfo.InvariantCulture);

        public string Identify() => Query("*IDN?");

        public void Initialize()
        {
            _session.Clear();
            Send("*RST");
            Send("*CLS");
        }

        public void Reset() => Send("*RST");

        // ---- IDcPowerSupply (acts on SelectedChannel) ---------------------------

        public void SetVoltage(double volts) => SetVoltage(SelectedChannel, volts);
        public void SetCurrentLimit(double amps) => SetCurrentLimit(SelectedChannel, amps);
        public void SetOutput(bool on) => SetOutput(SelectedChannel, on);
        public double MeasureVoltage() => MeasureVoltage(SelectedChannel);
        public double MeasureCurrent() => MeasureCurrent(SelectedChannel);

        // ---- channel-explicit -------------------------------------------------

        public void SetVoltage(int channel, double volts) =>
            Send($":SOUR{Check(channel)}:VOLT {F3(volts)}");

        public void SetCurrentLimit(int channel, double amps) =>
            Send($":SOUR{Check(channel)}:CURR {F3(amps)}");

        public void SetOutput(int channel, bool on) =>
            Send($":OUTP CH{Check(channel)},{(on ? "ON" : "OFF")}");

        public double MeasureVoltage(int channel) => ParseReading(Query($":MEAS:VOLT? CH{Check(channel)}"));
        public double MeasureCurrent(int channel) => ParseReading(Query($":MEAS:CURR? CH{Check(channel)}"));

        /// <summary>Measures the output power (W) on a channel (<c>:MEASure:POWEr? CH{n}</c>).</summary>
        public double MeasurePower(int channel) => ParseReading(Query($":MEAS:POWE? CH{Check(channel)}"));

        /// <summary>True if the channel's output is on (<c>:OUTPut? CH{n}</c>).</summary>
        public bool IsOutputOn(int channel) => ParseBoolean(Query($":OUTP? CH{Check(channel)}"));

        // ---- OVP / OCP --------------------------------------------------------

        public void SetOverVoltageProtection(int channel, double volts) =>
            Send($":SOUR{Check(channel)}:VOLT:PROT {F3(volts)}");

        public void SetOverVoltageProtectionEnabled(int channel, bool on) =>
            Send($":SOUR{Check(channel)}:VOLT:PROT:STAT {(on ? "ON" : "OFF")}");

        public void SetOverCurrentProtection(int channel, double amps) =>
            Send($":SOUR{Check(channel)}:CURR:PROT {F3(amps)}");

        public void SetOverCurrentProtectionEnabled(int channel, bool on) =>
            Send($":SOUR{Check(channel)}:CURR:PROT:STAT {(on ? "ON" : "OFF")}");

        /// <summary>Reads the next entry from the error queue (<c>:SYSTem:ERRor?</c>).</summary>
        public string NextError() => Query(":SYST:ERR?");

        internal static double ParseReading(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                throw new FormatException("Empty DP832 reading.");
            if (!double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                throw new FormatException($"Unrecognized DP832 reading: '{raw}'.");
            return v;
        }

        internal static bool ParseBoolean(string raw)
        {
            var s = (raw ?? string.Empty).Trim();
            return s.Equals("ON", StringComparison.OrdinalIgnoreCase) || s == "1"
                || s.Equals("YES", StringComparison.OrdinalIgnoreCase);
        }
    }
}
