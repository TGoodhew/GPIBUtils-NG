using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using GpibUtils.Visa.Simulation;

namespace GpibUtils.Instruments.Scopes
{
    /// <summary>
    /// An in-memory model of a Rigol DS1054Z for use with <see cref="SimulatedGpibProvider"/>. Records the
    /// SCPI the driver writes (run state, per-channel display) and answers the queries
    /// (<c>:MEASure:ITEM?</c>, <c>:TIMebase:MAIN:SCALe?</c>) so the <see cref="RigolDs1054Z"/> driver can be
    /// exercised with no hardware.
    /// </summary>
    public sealed class RigolDs1054ZSimulatedDevice
    {
        public SimulatedInstrument Instrument { get; }

        private readonly List<string> _commands = new List<string>();
        private readonly bool[] _channelOn = new bool[4];
        private readonly Dictionary<string, double> _measurements = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<string> Commands => _commands;

        /// <summary>Last run state: "RUN" / "STOP" / "SINGLE"; null before any.</summary>
        public string RunState { get; private set; }

        /// <summary>Timebase scale (s/div) reported by the query.</summary>
        public double TimebaseScale { get; set; } = 1e-3;

        public bool ChannelOn(int channel) => _channelOn[channel - 1];

        /// <summary>Sets the value a measurement item on a channel returns (key e.g. "VPP,CHANNEL1").</summary>
        public void SetMeasurement(string item, int channel, double value) =>
            _measurements[$"{item},CHANNEL{channel}"] = value;

        public RigolDs1054ZSimulatedDevice()
        {
            Instrument = new SimulatedInstrument
            {
                IdentificationString = "RIGOL TECHNOLOGIES,DS1054Z,0,00.04.04",
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

            if (upper == ":RUN") { RunState = "RUN"; return; }
            if (upper == ":STOP") { RunState = "STOP"; return; }
            if (upper == ":SINGLE") { RunState = "SINGLE"; return; }
            var m = Regex.Match(upper, @":CHANNEL(\d):DISPLAY (ON|OFF)");
            if (m.Success) _channelOn[int.Parse(m.Groups[1].Value) - 1] = m.Groups[2].Value == "ON";
        }

        private string Respond(string command)
        {
            var upper = (command ?? string.Empty).Trim().ToUpperInvariant();
            if (upper == ":TIMEBASE:MAIN:SCALE?") return Fmt(TimebaseScale);
            var m = Regex.Match(upper, @":MEASURE:ITEM\?\s*(.+)");
            if (m.Success)
            {
                var key = m.Groups[1].Value.Trim();
                return Fmt(_measurements.TryGetValue(key, out var v) ? v : 0.0);
            }
            return null;
        }

        private static string Fmt(double v) => v.ToString("+0.000000E+00;-0.000000E+00", CultureInfo.InvariantCulture);
    }
}
