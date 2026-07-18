using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GpibUtils.Pcl
{
    /// <summary>The kind of a parsed PCL operation.</summary>
    internal enum PclOpKind
    {
        Text, Cr, Lf, Ff, Bs, Ht, Reset,
        SetPitchCpi, SetLineSpacingLpi,
        SetRasterResolution, SetRasterCompression, StartRaster, RasterRow, EndRaster,
        MoveDotX, MoveDotY, MoveColumn, MoveRow
    }

    /// <summary>One parsed PCL operation (see <see cref="PclOpKind"/>).</summary>
    internal sealed class PclOp
    {
        public PclOpKind Kind;
        public string Text;    // Text
        public double Value;   // numeric parameter (cpi / lpi / dpi / dots / column / row / compression / start-mode)
        public byte[] Data;    // RasterRow payload

        public PclOp(PclOpKind kind) { Kind = kind; }
    }

    /// <summary>
    /// Parses an HP PCL byte stream (the ThinkJet subset) into a flat op list: printable text runs, the
    /// control bytes (CR/LF/FF/BS/HT), and the escape sequences this project emits/consumes — reset,
    /// pitch/line-spacing, and the raster-graphics group (<c>*t…R</c>, <c>*r…A</c>, <c>*b…M</c>, <c>*b…W</c>,
    /// <c>*r…B</c>). Handles combined parameterized sequences (e.g. <c>ESC*b1m512W</c>) and reads the binary
    /// payload that follows a <c>W</c> transfer. Unknown sequences are skipped, not fatal.
    /// </summary>
    internal static class PclParser
    {
        private const byte Esc = 0x1B;

        public static List<PclOp> Parse(byte[] data)
        {
            var ops = new List<PclOp>();
            if (data == null) return ops;

            var text = new StringBuilder();
            int i = 0;
            while (i < data.Length)
            {
                byte b = data[i];

                if (b == Esc) { FlushText(ops, text); i = ParseEscape(data, i + 1, ops); continue; }

                switch (b)
                {
                    case 0x0D: FlushText(ops, text); ops.Add(new PclOp(PclOpKind.Cr)); i++; continue;
                    case 0x0A: FlushText(ops, text); ops.Add(new PclOp(PclOpKind.Lf)); i++; continue;
                    case 0x0C: FlushText(ops, text); ops.Add(new PclOp(PclOpKind.Ff)); i++; continue;
                    case 0x08: FlushText(ops, text); ops.Add(new PclOp(PclOpKind.Bs)); i++; continue;
                    case 0x09: FlushText(ops, text); ops.Add(new PclOp(PclOpKind.Ht)); i++; continue;
                }

                if (b >= 0x20) text.Append((char)b);   // printable (Latin-1)
                i++;                                     // other control bytes are ignored
            }
            FlushText(ops, text);
            return ops;
        }

        private static void FlushText(List<PclOp> ops, StringBuilder text)
        {
            if (text.Length == 0) return;
            ops.Add(new PclOp(PclOpKind.Text) { Text = text.ToString() });
            text.Clear();
        }

        /// <summary>Parses one escape sequence starting just after the ESC; returns the index of the next byte.</summary>
        private static int ParseEscape(byte[] data, int i, List<PclOp> ops)
        {
            if (i >= data.Length) return i;
            byte c = data[i];

            // Two-character sequence: ESC + a final in 0x30-0x7E that is not a parameterized char (0x21-0x2F).
            if (c >= 0x30 && c <= 0x7E)
            {
                if (c == (byte)'E') ops.Add(new PclOp(PclOpKind.Reset));   // ESC E — printer reset
                return i + 1;                                              // other 2-char sequences: ignore
            }

            if (c < 0x21 || c > 0x2F) return i;   // not a valid parameterized start — skip the ESC
            byte param = c;                        // e.g. '&', '*', '('
            i++;
            if (i >= data.Length) return i;
            byte group = data[i];                  // e.g. 'l', 'k', 't', 'r', 'b', 'a', 'p'
            i++;

            // Combined sequence: [value][letter] pairs; lowercase letter = intermediate, uppercase = terminator.
            while (i < data.Length)
            {
                var sb = new StringBuilder();
                while (i < data.Length && IsValueChar(data[i])) { sb.Append((char)data[i]); i++; }
                if (i >= data.Length) break;

                byte letter = data[i];
                i++;
                char command = char.ToUpperInvariant((char)letter);
                double value = ParseValue(sb.ToString());

                // A 'W' transfer is followed by <value> binary bytes.
                if (command == 'W' && param == (byte)'*' && group == (byte)'b')
                {
                    int n = (int)value;
                    var payload = new byte[System.Math.Max(0, n)];
                    for (int k = 0; k < payload.Length && i < data.Length; k++) payload[k] = data[i++];
                    ops.Add(new PclOp(PclOpKind.RasterRow) { Data = payload });
                }
                else
                {
                    Emit(ops, param, group, command, value);
                }

                bool terminator = letter >= 0x40 && letter <= 0x5E;   // uppercase final ends the sequence
                if (terminator) break;
            }
            return i;
        }

        private static void Emit(List<PclOp> ops, byte param, byte group, char command, double value)
        {
            if (param == (byte)'&' && group == (byte)'k' && command == 'S')
                ops.Add(new PclOp(PclOpKind.SetPitchCpi) { Value = PitchToCpi(value) });
            else if (param == (byte)'&' && group == (byte)'l' && command == 'D')
                ops.Add(new PclOp(PclOpKind.SetLineSpacingLpi) { Value = value });
            else if (param == (byte)'&' && group == (byte)'a' && command == 'C')
                ops.Add(new PclOp(PclOpKind.MoveColumn) { Value = value });
            else if (param == (byte)'&' && group == (byte)'a' && command == 'R')
                ops.Add(new PclOp(PclOpKind.MoveRow) { Value = value });
            else if (param == (byte)'*' && group == (byte)'t' && command == 'R')
                ops.Add(new PclOp(PclOpKind.SetRasterResolution) { Value = value });
            else if (param == (byte)'*' && group == (byte)'r' && command == 'A')
                ops.Add(new PclOp(PclOpKind.StartRaster) { Value = value });   // 0 = left margin, 1 = cursor X
            else if (param == (byte)'*' && group == (byte)'r' && command == 'B')
                ops.Add(new PclOp(PclOpKind.EndRaster));
            else if (param == (byte)'*' && group == (byte)'b' && command == 'M')
                ops.Add(new PclOp(PclOpKind.SetRasterCompression) { Value = value });
            else if (param == (byte)'*' && group == (byte)'p' && command == 'X')
                ops.Add(new PclOp(PclOpKind.MoveDotX) { Value = value });
            else if (param == (byte)'*' && group == (byte)'p' && command == 'Y')
                ops.Add(new PclOp(PclOpKind.MoveDotY) { Value = value });
            // anything else: silently ignored
        }

        /// <summary>ThinkJet pitch code (<c>ESC&amp;k#S</c>) → characters per inch. 0 = 10 cpi (pica),
        /// 2 = 16.67 cpi (compressed), 4 = 5 cpi (expanded); anything else falls back to 10 cpi.</summary>
        private static double PitchToCpi(double code)
        {
            int n = (int)code;
            switch (n)
            {
                case 2: return 16.67;
                case 4: return 5.0;
                default: return 10.0;
            }
        }

        private static bool IsValueChar(byte b) =>
            (b >= (byte)'0' && b <= (byte)'9') || b == (byte)'+' || b == (byte)'-' || b == (byte)'.';

        private static double ParseValue(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
        }
    }
}
