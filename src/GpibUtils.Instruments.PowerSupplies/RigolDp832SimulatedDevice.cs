using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using GpibUtils.Visa.Simulation;

namespace GpibUtils.Instruments.PowerSupplies
{
    /// <summary>
    /// An in-memory model of a Rigol DP832 for use with <see cref="SimulatedGpibProvider"/>, rich enough to
    /// drive the <see cref="RigolDp832"/> driver end to end with no hardware. It decodes the per-channel
    /// SCPI (<c>:SOUR{n}:VOLT/CURR</c>, <c>:OUTP CH{n},ON/OFF</c>, protection) into per-channel state and
    /// answers the measurement queries (<c>:MEAS:VOLT?/CURR?/POWE? CH{n}</c>, <c>:OUTP? CH{n}</c>).
    /// </summary>
    public sealed class RigolDp832SimulatedDevice
    {
        private sealed class Channel
        {
            public double Voltage, CurrentLimit, Ovp, Ocp, LoadCurrent;
            public bool OutputOn, OvpEnabled, OcpEnabled;
        }

        /// <summary>The <see cref="SimulatedInstrument"/> to register with a <see cref="SimulatedGpibProvider"/>.</summary>
        public SimulatedInstrument Instrument { get; }

        private readonly Channel[] _ch = { new Channel(), new Channel(), new Channel() };
        private readonly List<string> _commands = new List<string>();

        /// <summary>Every command sent (writes and queries), in order (for assertions).</summary>
        public IReadOnlyList<string> Commands => _commands;

        public double Voltage(int channel) => _ch[channel - 1].Voltage;
        public double CurrentLimit(int channel) => _ch[channel - 1].CurrentLimit;
        public bool OutputOn(int channel) => _ch[channel - 1].OutputOn;
        public double Ovp(int channel) => _ch[channel - 1].Ovp;
        public bool OvpEnabled(int channel) => _ch[channel - 1].OvpEnabled;

        /// <summary>Sets the load current a channel reports on <c>:MEAS:CURR?/POWE?</c> while its output is on.</summary>
        public void SetLoadCurrent(int channel, double amps) => _ch[channel - 1].LoadCurrent = amps;

        public RigolDp832SimulatedDevice()
        {
            Instrument = new SimulatedInstrument
            {
                IdentificationString = "RIGOL TECHNOLOGIES,DP832,0,1.0",
                WriteObserver = Apply,
                Responder = Respond
            };
        }

        private void Apply(string command)
        {
            var cmd = command.Trim();
            if (cmd.Length == 0) return;
            _commands.Add(cmd);
            var upper = cmd.ToUpperInvariant();
            if (upper.EndsWith("?")) return;   // queries answered in Respond

            if (upper == "*RST") { for (int i = 0; i < _ch.Length; i++) _ch[i] = new Channel(); return; }

            var srcCh = Regex.Match(upper, @"^:SOUR(\d)");
            var outCh = Regex.Match(upper, @":OUTP\s+CH(\d)");

            if (srcCh.Success)
            {
                var c = _ch[int.Parse(srcCh.Groups[1].Value) - 1];
                if (upper.Contains("VOLT:PROT:STAT")) c.OvpEnabled = upper.TrimEnd().EndsWith("ON");
                else if (upper.Contains("CURR:PROT:STAT")) c.OcpEnabled = upper.TrimEnd().EndsWith("ON");
                else if (upper.Contains("VOLT:PROT")) c.Ovp = Arg(cmd) ?? c.Ovp;
                else if (upper.Contains("CURR:PROT")) c.Ocp = Arg(cmd) ?? c.Ocp;
                else if (upper.Contains(":VOLT")) c.Voltage = Arg(cmd) ?? c.Voltage;
                else if (upper.Contains(":CURR")) c.CurrentLimit = Arg(cmd) ?? c.CurrentLimit;
                return;
            }
            if (outCh.Success)
            {
                var c = _ch[int.Parse(outCh.Groups[1].Value) - 1];
                c.OutputOn = upper.TrimEnd().EndsWith("ON");
            }
        }

        private string Respond(string command)
        {
            var raw = (command ?? string.Empty).Trim();
            var upper = raw.ToUpperInvariant();
            var m = Regex.Match(upper, @"CH(\d)");
            if (m.Success)
            {
                var c = _ch[int.Parse(m.Groups[1].Value) - 1];
                if (upper.StartsWith(":MEAS:VOLT?")) return Fmt(c.OutputOn ? c.Voltage : 0.0);
                if (upper.StartsWith(":MEAS:CURR?")) return Fmt(c.OutputOn ? c.LoadCurrent : 0.0);
                if (upper.StartsWith(":MEAS:POWE?")) return Fmt(c.OutputOn ? c.Voltage * c.LoadCurrent : 0.0);
                if (upper.StartsWith(":OUTP?")) return c.OutputOn ? "ON" : "OFF";
            }
            if (upper == ":SYST:ERR?") return "0,\"No error\"";
            return null;
        }

        private static string Fmt(double v) => v.ToString("F4", CultureInfo.InvariantCulture);

        // The numeric argument is the whitespace-separated token after the SCPI keyword (the channel digit
        // is embedded IN the keyword, e.g. ":SOUR2:VOLT 12.000" -> "12.000", not "2").
        private static double? Arg(string s)
        {
            int sp = s.LastIndexOf(' ');
            if (sp < 0) return null;
            return double.TryParse(s.Substring(sp + 1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                ? v : (double?)null;
        }
    }
}
