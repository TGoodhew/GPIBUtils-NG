using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.Printers
{
    /// <summary>
    /// Driver for the HP 2225 ThinkJet — an HP-IB inkjet printer driven with the PCL escape set. Prints plain
    /// text and PCL raster graphics over any <see cref="IInstrumentSession"/> (issue #166). The PCL it emits is
    /// exactly what <c>GpibUtils.Pcl.PclRenderer</c> parses, so a page can be previewed or round-tripped
    /// without hardware.
    ///
    /// <para>Because <see cref="PrintRaster"/> takes a <see cref="Bitmap"/>, the same content the HP-GL
    /// plotters draw can be printed here by rasterizing it first (<c>HpglRenderer.RenderToBitmap</c>) — the
    /// bridge that makes plotter, ThinkJet and Windows-printer interchangeable hardcopy sinks.</para>
    /// </summary>
    public sealed class Hp2225A : IPrinter
    {
        /// <summary>GPIB address of the ThinkJet — factory default 1. Override with <c>--address</c>.</summary>
        public const string DefaultResource = "GPIB0::1::INSTR";

        /// <summary>Luminance below which a raster pixel is treated as ink (0–255).</summary>
        public const int InkThreshold = 128;

        private static readonly Encoding Latin1 = Encoding.GetEncoding("ISO-8859-1");
        private const char Esc = '';

        private readonly IInstrumentSession _session;
        private readonly List<byte> _sent = new List<byte>();
        private int _dpi = 96;

        public Hp2225A(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;

        /// <summary>Every byte streamed to the printer, in order (for CLI echo / tests / round-tripping).</summary>
        public IReadOnlyList<byte> Sent => _sent;

        private void Send(byte[] bytes) { _session.WriteBytes(bytes); _sent.AddRange(bytes); }
        private void Send(string ascii) => Send(Latin1.GetBytes(ascii));

        /// <summary>The ThinkJet has no identifying query; returns a fixed descriptor.</summary>
        public string Identify() => "HP 2225 ThinkJet (HP-IB printer, PCL)";

        /// <summary>Device clear + printer reset (<c>ESC E</c>).</summary>
        public void Initialize() { _session.Clear(); Send(Esc + "E"); }

        /// <summary>Form feed (0x0C) — ejects the page.</summary>
        public void FormFeed() => Send(new byte[] { 0x0C });

        /// <summary>Sets the raster resolution (<c>ESC*t&lt;dpi&gt;R</c>); the ThinkJet supports 96 and 192.</summary>
        public void SetResolutionDpi(int dpi)
        {
            if (dpi <= 0) throw new ArgumentOutOfRangeException(nameof(dpi), dpi, "DPI must be positive.");
            _dpi = dpi;
            Send(Esc + "*t" + dpi.ToString(CultureInfo.InvariantCulture) + "R");
        }

        /// <summary>Prints plain text; bare newlines are normalized to CR+LF for the printer.</summary>
        public void PrintText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            var normalized = text.Replace("\r\n", "\n").Replace("\n", "\r\n");
            Send(normalized);
        }

        /// <summary>Prints a bitmap as PCL raster graphics (1-bpp, MSB = leftmost dot; a dark pixel inks).</summary>
        public void PrintRaster(Bitmap image)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            int w = image.Width, h = image.Height;
            int bytesPerRow = (w + 7) / 8;

            Send(Esc + "*t" + _dpi.ToString(CultureInfo.InvariantCulture) + "R");   // resolution
            Send(Esc + "*r0A");                                                     // start raster at left margin
            var row = new byte[bytesPerRow];
            for (int y = 0; y < h; y++)
            {
                Array.Clear(row, 0, row.Length);
                for (int x = 0; x < w; x++)
                {
                    var c = image.GetPixel(x, y);
                    // Treat transparent as paper; ink when the (opaque) pixel is dark.
                    if (c.A >= InkThreshold && (c.R * 299 + c.G * 587 + c.B * 114) / 1000 < InkThreshold)
                        row[x / 8] |= (byte)(0x80 >> (x % 8));
                }
                Send(Esc + "*b" + bytesPerRow.ToString(CultureInfo.InvariantCulture) + "W");
                Send((byte[])row.Clone());
            }
            Send(Esc + "*rB");                                                      // end raster
        }
    }
}
