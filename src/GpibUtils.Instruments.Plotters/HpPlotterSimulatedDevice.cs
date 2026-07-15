using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using GpibUtils.Visa.Simulation;

namespace GpibUtils.Instruments.Plotters
{
    /// <summary>
    /// An in-memory model of an HP 7090A/7475A/7550A pen plotter for use with
    /// <see cref="SimulatedGpibProvider"/>, rich enough to drive the <see cref="HpPlotter"/> driver end to
    /// end with no hardware. It records the HP-GL the driver streams, tracks pen state / position / selected
    /// pen / page count, and answers the plotter's output queries (<c>OI;</c>/<c>OW;</c>/<c>OP;</c>).
    /// </summary>
    public sealed class HpPlotterSimulatedDevice
    {
        public SimulatedInstrument Instrument { get; }

        private readonly List<string> _commands = new List<string>();

        /// <summary>Every HP-GL instruction the plotter was sent (writes and queries), in order.</summary>
        public IReadOnlyList<string> Commands => _commands;

        /// <summary>Model id returned by <c>OI;</c> (e.g. "7090A").</summary>
        public string ModelId { get; set; } = "7090A";

        /// <summary>Hard-clip window returned by <c>OW;</c> — [Xll, Yll, Xur, Yur].</summary>
        public int[] HardClipWindow { get; set; } = { 0, 0, 10300, 7650 };

        /// <summary>Scaling points returned by <c>OP;</c> — [P1x, P1y, P2x, P2y].</summary>
        public int[] ScalingPoints { get; set; } = { 0, 0, 10300, 7650 };

        public bool PenDown { get; private set; }
        public int SelectedPen { get; private set; }
        public int X { get; private set; }
        public int Y { get; private set; }
        public int PageCount { get; private set; }
        public string LastLabel { get; private set; }

        public HpPlotterSimulatedDevice()
        {
            Instrument = new SimulatedInstrument
            {
                IdentificationString = "HP,7090A,0,1.0",   // plotters have no *IDN?; OI is used instead
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

            if (upper.StartsWith("IN")) { PenDown = false; SelectedPen = 0; X = Y = 0; return; }
            if (upper.StartsWith("SP")) { SelectedPen = ParseInt(upper.Substring(2), SelectedPen); return; }
            if (upper.StartsWith("PU")) { PenDown = false; ApplyMove(upper.Substring(2), absolute: true); return; }
            if (upper.StartsWith("PD")) { PenDown = true; ApplyMove(upper.Substring(2), absolute: true); return; }
            if (upper.StartsWith("PA")) { ApplyMove(upper.Substring(2), absolute: true); return; }
            if (upper.StartsWith("PR")) { ApplyMove(upper.Substring(2), absolute: false); return; }
            if (upper.StartsWith("PG")) { PageCount++; return; }
            if (upper.StartsWith("LB")) { LastLabel = cmd.Substring(2).TrimEnd((char)3, ';'); return; }
        }

        private void ApplyMove(string args, bool absolute)
        {
            var nums = Regex.Matches(args, @"-?\d+");
            for (int i = 0; i + 1 < nums.Count; i += 2)
            {
                int a = int.Parse(nums[i].Value, CultureInfo.InvariantCulture);
                int b = int.Parse(nums[i + 1].Value, CultureInfo.InvariantCulture);
                if (absolute) { X = a; Y = b; } else { X += a; Y += b; }
            }
        }

        private string Respond(string command)
        {
            var upper = (command ?? string.Empty).Trim().ToUpperInvariant();
            if (upper.StartsWith("OI")) return ModelId;
            if (upper.StartsWith("OW")) return string.Join(",", HardClipWindow);
            if (upper.StartsWith("OP")) return string.Join(",", ScalingPoints);
            return null;
        }

        private static int ParseInt(string s, int fallback)
        {
            var m = Regex.Match(s ?? string.Empty, @"-?\d+");
            return m.Success ? int.Parse(m.Value, CultureInfo.InvariantCulture) : fallback;
        }
    }
}
