using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace GpibUtils.Pcl
{
    /// <summary>
    /// Renders an HP PCL stream (the ThinkJet subset — see <see cref="PclParser"/>) to a raster
    /// (<see cref="Bitmap"/> or PNG bytes) for on-screen preview and for printing to a normal Windows printer.
    /// Text is laid out on the PCL character grid (pitch / line-spacing) with a monospace font; raster
    /// graphics (<c>ESC*r/ESC*b</c>, uncompressed and run-length) are blitted dot-for-dot.
    /// </summary>
    public static class PclRenderer
    {
        /// <summary>Renders a PCL byte stream to a <see cref="Bitmap"/> (caller owns/disposes it).</summary>
        public static Bitmap RenderToBitmap(byte[] pcl, PclRenderOptions options = null)
        {
            options = options ?? new PclRenderOptions();
            var ops = PclParser.Parse(pcl);

            int width = Math.Max(1, options.PageWidthDots);
            int maxH = Math.Max(1, options.MaxHeightDots);
            var full = new Bitmap(width, maxH, PixelFormat.Format32bppArgb);
            int contentBottom;
            try
            {
                using (var g = Graphics.FromImage(full))
                {
                    g.SmoothingMode = SmoothingMode.None;
                    g.TextRenderingHint = options.Antialias
                        ? System.Drawing.Text.TextRenderingHint.AntiAlias
                        : System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
                    g.Clear(options.ResolveBackground());
                    contentBottom = Execute(ops, g, full, options);
                }

                int cropH = Math.Min(maxH, Math.Max(1, contentBottom + options.Margin));
                using (full)
                    return full.Clone(new Rectangle(0, 0, width, cropH), PixelFormat.Format32bppArgb);
            }
            catch
            {
                full.Dispose();
                throw;
            }
        }

        /// <summary>Renders a PCL byte stream and encodes the result as PNG bytes.</summary>
        public static byte[] RenderToPng(byte[] pcl, PclRenderOptions options = null)
        {
            using (var bmp = RenderToBitmap(pcl, options))
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }

        // --- interpretation ---------------------------------------------------

        private static int Execute(List<PclOp> ops, Graphics g, Bitmap bmp, PclRenderOptions opt)
        {
            int dpi = opt.DefaultDpi;
            double cpi = 10.0, lpi = 6.0;
            int margin = opt.Margin;
            int x = margin, y = margin;
            int maxBottom = margin;
            var ink = opt.ResolveInk();

            bool inRaster = false;
            int rasterLeft = margin, rasterY = margin, compression = 0;

            int CellW() => Math.Max(1, (int)Math.Round(dpi / cpi));
            int LineH() => Math.Max(1, (int)Math.Round(dpi / lpi));

            using (var inkBrush = new SolidBrush(ink))
            {
                foreach (var op in ops)
                {
                    switch (op.Kind)
                    {
                        case PclOpKind.Reset: cpi = 10.0; lpi = 6.0; x = margin; y = margin; inRaster = false; break;
                        case PclOpKind.SetPitchCpi: if (op.Value > 0) cpi = op.Value; break;
                        case PclOpKind.SetLineSpacingLpi: if (op.Value > 0) lpi = op.Value; break;
                        case PclOpKind.SetRasterResolution: if (op.Value > 0) dpi = (int)op.Value; break;
                        case PclOpKind.SetRasterCompression: compression = (int)op.Value; break;

                        case PclOpKind.Cr: x = margin; break;
                        case PclOpKind.Lf: y += LineH(); break;
                        case PclOpKind.Ff: y += 4 * LineH(); x = margin; break;   // page break (approx, for preview)
                        case PclOpKind.Bs: x = Math.Max(margin, x - CellW()); break;
                        case PclOpKind.Ht: x += CellW() * (8 - (((x - margin) / CellW()) % 8)); break;

                        case PclOpKind.MoveColumn: x = margin + (int)op.Value * CellW(); break;
                        case PclOpKind.MoveRow: y = margin + (int)op.Value * LineH(); break;
                        case PclOpKind.MoveDotX: x = margin + (int)op.Value; break;
                        case PclOpKind.MoveDotY: y = margin + (int)op.Value; break;

                        case PclOpKind.Text:
                            DrawText(g, inkBrush, op.Text, ref x, y, CellW(), LineH(), dpi, cpi, opt);
                            break;

                        case PclOpKind.StartRaster:
                            inRaster = true;
                            rasterLeft = (int)op.Value == 1 ? x : margin;
                            rasterY = y;
                            break;
                        case PclOpKind.RasterRow:
                            if (inRaster) { BlitRasterRow(bmp, op.Data, compression, rasterLeft, rasterY, ink); rasterY++; }
                            break;
                        case PclOpKind.EndRaster:
                            inRaster = false; y = rasterY; x = margin;
                            break;
                    }

                    int bottom = inRaster ? rasterY : y + LineH();
                    if (bottom > maxBottom) maxBottom = bottom;
                }
            }
            return maxBottom;
        }

        private static void DrawText(Graphics g, Brush ink, string text, ref int x, int y,
            int cellW, int lineH, int dpi, double cpi, PclRenderOptions opt)
        {
            if (string.IsNullOrEmpty(text)) return;
            // Monospace font sized to the character cell; draw char-by-char to hold the PCL grid exactly.
            float emPx = Math.Max(4f, lineH * 0.82f);
            using (var font = new Font("Consolas", emPx, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                var fmt = StringFormat.GenericTypographic;
                foreach (char ch in text)
                {
                    if (ch >= 0x20) g.DrawString(ch.ToString(), font, ink, x, y, fmt);
                    x += cellW;
                }
            }
        }

        /// <summary>Blits one raster row (MSB = leftmost dot, 1 = ink) at (left, row). Expands run-length
        /// (compression mode 1) first; other modes are treated as uncompressed.</summary>
        private static void BlitRasterRow(Bitmap bmp, byte[] data, int compression, int left, int row, Color ink)
        {
            if (data == null || row < 0 || row >= bmp.Height) return;
            byte[] bytes = compression == 1 ? ExpandRunLength(data) : data;
            int argb = ink.ToArgb();
            for (int bi = 0; bi < bytes.Length; bi++)
            {
                byte v = bytes[bi];
                if (v == 0) continue;
                for (int bit = 0; bit < 8; bit++)
                {
                    if ((v & (0x80 >> bit)) == 0) continue;
                    int px = left + bi * 8 + bit;
                    if (px >= 0 && px < bmp.Width) bmp.SetPixel(px, row, Color.FromArgb(argb));
                }
            }
        }

        /// <summary>Expands PCL run-length data: (count, value) pairs, value repeated count+1 times.</summary>
        internal static byte[] ExpandRunLength(byte[] data)
        {
            var outBytes = new List<byte>(data.Length * 2);
            for (int k = 0; k + 1 < data.Length; k += 2)
            {
                int rep = data[k] + 1;
                byte val = data[k + 1];
                for (int r = 0; r < rep; r++) outBytes.Add(val);
            }
            return outBytes.ToArray();
        }

        /// <summary>UTF-safe decode of a text string to a ThinkJet-ready PCL byte stream (Latin-1).</summary>
        public static byte[] EncodeText(string text) =>
            Encoding.GetEncoding("ISO-8859-1").GetBytes(text ?? string.Empty);
    }
}
