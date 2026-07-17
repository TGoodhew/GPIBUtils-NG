using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using GpibUtils.Visa.Simulation;

namespace GpibUtils.Instruments.SignalSources
{
    /// <summary>
    /// An in-memory model of a Rigol DG1000Z for use with <see cref="SimulatedGpibProvider"/>: decodes the
    /// per-channel SCPI setup the <see cref="RigolDg1000Z"/> driver writes (<c>:SOURn:FUNC/FREQ/VOLT</c>,
    /// <c>:OUTPn</c>) and answers <c>*IDN?</c> plus the matching queries. Two channels. No hardware.
    /// </summary>
    public sealed class RigolDg1000ZSimulatedDevice
    {
        public SimulatedInstrument Instrument { get; }

        private readonly List<string> _commands = new List<string>();
        private readonly string[] _shape = { "SIN", "SIN" };
        private readonly double[] _freq = { 1000, 1000 };
        private readonly double[] _amp = { 5, 5 };
        private readonly double[] _offset = { 0, 0 };
        private readonly bool[] _output = { false, false };

        public IReadOnlyList<string> Commands => _commands;
        public string Shape(int ch) => _shape[Idx(ch)];
        public double Frequency(int ch) => _freq[Idx(ch)];
        public double Amplitude(int ch) => _amp[Idx(ch)];
        public double Offset(int ch) => _offset[Idx(ch)];
        public bool OutputOn(int ch) => _output[Idx(ch)];

        private static int Idx(int ch) => ch == 2 ? 1 : 0;

        public RigolDg1000ZSimulatedDevice()
        {
            Instrument = new SimulatedInstrument
            {
                IdentificationString = "Rigol Technologies,DG1062Z,DG0000000,00.01",
                WriteObserver = Apply,
                Responder = Respond
            };
        }

        private void Apply(string command)
        {
            var raw = (command ?? string.Empty).Trim();
            if (raw.Length == 0) return;
            _commands.Add(raw);
            var upper = raw.ToUpperInvariant();

            var m = Regex.Match(upper, @"^:?SOUR(\d):(FUNC|FREQ|VOLT:OFFS|VOLT)\s+(.+)$");
            if (m.Success)
            {
                int i = Idx(int.Parse(m.Groups[1].Value));
                string arg = m.Groups[3].Value.Trim();
                switch (m.Groups[2].Value)
                {
                    case "FUNC": _shape[i] = arg; break;
                    case "FREQ": _freq[i] = ParseD(arg); break;
                    case "VOLT": _amp[i] = ParseD(arg); break;
                    case "VOLT:OFFS": _offset[i] = ParseD(arg); break;
                }
                return;
            }
            var o = Regex.Match(upper, @"^:?OUTP(\d)\s+(ON|OFF|1|0)$");
            if (o.Success) { _output[Idx(int.Parse(o.Groups[1].Value))] = o.Groups[2].Value == "ON" || o.Groups[2].Value == "1"; }
        }

        private string Respond(string command)
        {
            var upper = (command ?? string.Empty).Trim().ToUpperInvariant();
            if (upper == "*IDN?") return Instrument.IdentificationString;
            var m = Regex.Match(upper, @"^:?SOUR(\d):(FUNC|FREQ|VOLT:OFFS|VOLT)\?$");
            if (m.Success)
            {
                int i = Idx(int.Parse(m.Groups[1].Value));
                switch (m.Groups[2].Value)
                {
                    case "FUNC": return _shape[i];
                    case "FREQ": return _freq[i].ToString("E6", CultureInfo.InvariantCulture);
                    case "VOLT": return _amp[i].ToString("E6", CultureInfo.InvariantCulture);
                    case "VOLT:OFFS": return _offset[i].ToString("E6", CultureInfo.InvariantCulture);
                }
            }
            var o = Regex.Match(upper, @"^:?OUTP(\d)\?$");
            if (o.Success) return _output[Idx(int.Parse(o.Groups[1].Value))] ? "ON" : "OFF";
            return null;
        }

        private static double ParseD(string s) =>
            double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }
}
