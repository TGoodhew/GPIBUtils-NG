using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GpibUtils.Hpgl;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.Plotters
{
    /// <summary>The HP pen-plotter model — controls the paper-feed capability.</summary>
    public enum HpPlotterModel
    {
        /// <summary>HP 7090A Measurement Plotting System (roll paper, no auto-feed).</summary>
        Hp7090A,

        /// <summary>HP 7475A 6-pen graphics plotter (single-sheet, no auto-feed).</summary>
        Hp7475A,

        /// <summary>HP 7550A high-speed graphics plotter (auto paper feed).</summary>
        Hp7550A
    }

    /// <summary>
    /// Driver for the HP 7090A / 7475A / 7550A HP-GL pen plotters — the canonical plotter driver,
    /// consolidating the <c>7090ATest</c> (#38), <c>7550ATest</c> (#39) and <c>HPGLTest</c> streamer (#40)
    /// apps. Streams HP-GL to draw (pen select / up-down / absolute + relative moves / labels), advances
    /// paper on the auto-feed 7550A, and reads the plotter's scaling/clip points. Runs over any
    /// <see cref="IInstrumentSession"/>.
    ///
    /// <para>The 7090A and 7475A are functionally identical for basic plotting and need paper loaded by hand;
    /// the 7550A auto-feeds (<c>PG;</c> advance, <c>NR</c> unload). A full HP-GL document can be previewed to a
    /// PNG without hardware via <see cref="RenderPreview"/> (the shared #42 <see cref="HpglRenderer"/>).</para>
    /// </summary>
    public sealed class HpPlotter : IPlotter
    {
        /// <summary>Default VISA resource — the legacy plotter apps' address (GPIB 6). Override with
        /// <c>--address</c>. (HP plotters ship factory-set to address 5; the bench used 6.)</summary>
        public const string DefaultResource = "GPIB0::6::INSTR";

        /// <summary>HP-GL label terminator (ETX, 0x03).</summary>
        private const char Etx = '\u0003'; // HP-GL label terminator (ETX)

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public HpPlotterModel Model { get; }

        public HpPlotter(IInstrumentSession session, HpPlotterModel model = HpPlotterModel.Hp7090A)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            Model = model;
        }

        public string ResourceName => _session.ResourceName;

        /// <summary>Every command sent through the driver, in order (for CLI echo / tests).</summary>
        public IReadOnlyList<string> History => _history;

        /// <summary>Only the 7550A auto-loads/advances paper.</summary>
        public bool AutoFeed => Model == HpPlotterModel.Hp7550A;

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

        public string Identify() => Query("OI;");

        public void Initialize()
        {
            _session.Clear();   // HP-IB device clear
            Send("IN;");        // initialize HP-GL to defaults
        }

        public void SelectPen(int pen)
        {
            if (pen < 0 || pen > 8)
                throw new ArgumentOutOfRangeException(nameof(pen), pen, "Pen must be 0–8 (0 = store pen).");
            Send("SP" + pen.ToString(CultureInfo.InvariantCulture) + ";");
        }

        public void PenUp() => Send("PU;");
        public void PenDown() => Send("PD;");

        public void MoveTo(int x, int y) =>
            Send("PA" + x.ToString(CultureInfo.InvariantCulture) + "," + y.ToString(CultureInfo.InvariantCulture) + ";");

        /// <summary>Moves the pen by a relative offset in plotter units (<c>PR dx,dy;</c>).</summary>
        public void MoveBy(int dx, int dy) =>
            Send("PR" + dx.ToString(CultureInfo.InvariantCulture) + "," + dy.ToString(CultureInfo.InvariantCulture) + ";");

        /// <summary>Draws a straight line between two absolute points (pen up to the start, down to the end).</summary>
        public void Line(int x1, int y1, int x2, int y2)
        {
            PenUp(); MoveTo(x1, y1); PenDown(); MoveTo(x2, y2); PenUp();
        }

        public void Label(string text) => Send("LB" + (text ?? string.Empty) + Etx);

        /// <summary>
        /// Streams a raw HP-GL document to the plotter. The document is split into individual instructions
        /// (on the <c>;</c> terminator) and written one at a time — matching the legacy apps, which do this so
        /// a long plot never overruns the plotter's input buffer.
        /// </summary>
        public void PlotHpgl(string hpgl)
        {
            if (string.IsNullOrWhiteSpace(hpgl)) return;
            foreach (var instruction in SplitInstructions(hpgl))
                Send(instruction);
        }

        /// <summary>Advances to a fresh page. Auto-feed only (7550A, <c>PG;</c>); throws on manual-feed models
        /// (load paper by hand instead).</summary>
        public void AdvancePage()
        {
            if (!AutoFeed)
                throw new InvalidOperationException($"{Model} has no auto paper feed — load/advance paper by hand.");
            Send("PG;");
        }

        /// <summary>Unloads the paper (7550A only, <c>NR</c>).</summary>
        public void UnloadPaper()
        {
            if (!AutoFeed)
                throw new InvalidOperationException($"{Model} has no auto paper feed — remove paper by hand.");
            Send("NR;");
        }

        /// <summary>Reads the P1/P2 scaling points (<c>OP;</c>) as [P1x, P1y, P2x, P2y] plotter units.</summary>
        public int[] OutputScalingPoints() => ParsePoints(Query("OP;"), "OP", 4);

        /// <summary>Reads the hard-clip output window (<c>OW;</c>) as [Xll, Yll, Xur, Yur] plotter units.</summary>
        public int[] OutputWindow() => ParsePoints(Query("OW;"), "OW", 4);

        /// <summary>Renders an HP-GL document to a PNG for a hardware-free preview, via the shared #42 renderer.</summary>
        public static byte[] RenderPreview(string hpgl) => HpglRenderer.RenderToPng(hpgl ?? string.Empty);

        // ---- helpers --------------------------------------------------------

        /// <summary>Splits an HP-GL stream into instructions, each ending in its <c>;</c> terminator; the
        /// terminator-less <c>LB…ETX</c> label instruction is kept whole.</summary>
        internal static IEnumerable<string> SplitInstructions(string hpgl)
        {
            var result = new List<string>();
            int i = 0, n = hpgl.Length;
            while (i < n)
            {
                int start = i;
                // A label runs to the ETX terminator, not a semicolon.
                if (i + 1 < n && char.ToUpperInvariant(hpgl[i]) == 'L' && char.ToUpperInvariant(hpgl[i + 1]) == 'B')
                {
                    i += 2;
                    while (i < n && hpgl[i] != Etx) i++;
                    if (i < n) i++; // consume ETX
                    var label = hpgl.Substring(start, i - start).Trim();
                    if (label.Length > 0) result.Add(label);
                    continue;
                }
                while (i < n && hpgl[i] != ';') i++;
                if (i < n) i++; // consume ';'
                var instr = hpgl.Substring(start, i - start).Trim();
                if (instr.Length > 0) result.Add(instr);
            }
            return result;
        }

        internal static int[] ParsePoints(string raw, string what, int expected)
        {
            if (string.IsNullOrWhiteSpace(raw))
                throw new FormatException($"Empty plotter {what} response.");
            var parts = raw.Split(new[] { ',', ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var values = new List<int>();
            foreach (var p in parts)
            {
                if (!double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    throw new FormatException($"Unrecognized plotter {what} value: '{p}'.");
                values.Add((int)Math.Round(v));
            }
            if (values.Count != expected)
                throw new FormatException($"Plotter {what} returned {values.Count} value(s), expected {expected}: '{raw}'.");
            return values.ToArray();
        }
    }
}
