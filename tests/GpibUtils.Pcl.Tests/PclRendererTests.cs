using System.Collections.Generic;
using System.Drawing;
using GpibUtils.Pcl;
using Xunit;

namespace GpibUtils.Pcl.Tests
{
    public class PclRendererTests
    {
        private const byte Esc = 0x1B;

        private static byte[] Bytes(params object[] parts)
        {
            var list = new List<byte>();
            foreach (var p in parts)
            {
                if (p is byte b) list.Add(b);
                if (p is int n) list.Add((byte)n);
                if (p is string s) foreach (var c in s) list.Add((byte)c);
                if (p is byte[] arr) list.AddRange(arr);
            }
            return list.ToArray();
        }

        // ---- parser ----

        [Fact]
        public void Parses_reset_and_text()
        {
            var ops = PclParser.Parse(Bytes(Esc, "E", "Hi"));
            Assert.Equal(PclOpKind.Reset, ops[0].Kind);
            Assert.Equal(PclOpKind.Text, ops[1].Kind);
            Assert.Equal("Hi", ops[1].Text);
        }

        [Fact]
        public void Parses_pitch_and_line_spacing()
        {
            var ops = PclParser.Parse(Bytes(Esc, "&k2S", Esc, "&l8D"));
            Assert.Equal(PclOpKind.SetPitchCpi, ops[0].Kind);
            Assert.Equal(16.67, ops[0].Value, 2);           // compressed pitch
            Assert.Equal(PclOpKind.SetLineSpacingLpi, ops[1].Kind);
            Assert.Equal(8.0, ops[1].Value, 3);
        }

        [Fact]
        public void Parses_raster_transfer_reading_the_binary_payload()
        {
            // ESC*t96R ESC*r1A ESC*b2W <0xFF,0x00> ESC*rB
            var ops = PclParser.Parse(Bytes(Esc, "*t96R", Esc, "*r1A", Esc, "*b2W", new byte[] { 0xFF, 0x00 }, Esc, "*rB"));
            Assert.Equal(PclOpKind.SetRasterResolution, ops[0].Kind);
            Assert.Equal(96, ops[0].Value);
            Assert.Equal(PclOpKind.StartRaster, ops[1].Kind);
            Assert.Equal(1, ops[1].Value);
            var row = Assert.Single(ops, o => o.Kind == PclOpKind.RasterRow);
            Assert.Equal(new byte[] { 0xFF, 0x00 }, row.Data);
            Assert.Contains(ops, o => o.Kind == PclOpKind.EndRaster);
        }

        [Fact]
        public void Parses_combined_sequence_compression_then_transfer()
        {
            // ESC*b1m2W<data> — mode 1 (RLE) then a 2-byte transfer, one combined sequence.
            var ops = PclParser.Parse(Bytes(Esc, "*b1m2W", new byte[] { 0x01, 0xFF }));
            Assert.Equal(PclOpKind.SetRasterCompression, ops[0].Kind);
            Assert.Equal(1, ops[0].Value);
            Assert.Equal(PclOpKind.RasterRow, ops[1].Kind);
            Assert.Equal(new byte[] { 0x01, 0xFF }, ops[1].Data);
        }

        // ---- run-length ----

        [Fact]
        public void Run_length_expands_count_plus_one()
        {
            // (count=1 -> 2 copies of 0xAA), (count=0 -> 1 copy of 0x0F)
            Assert.Equal(new byte[] { 0xAA, 0xAA, 0x0F }, PclRenderer.ExpandRunLength(new byte[] { 0x01, 0xAA, 0x00, 0x0F }));
        }

        // ---- renderer ----

        [Fact]
        public void Renders_text_to_ink_pixels_on_white()
        {
            using (var bmp = PclRenderer.RenderToBitmap(Bytes(Esc, "E", "TEST")))
            {
                Assert.True(bmp.Width > 0 && bmp.Height > 0);
                Assert.True(HasNonWhitePixel(bmp), "expected rendered text to leave ink pixels");
            }
        }

        [Fact]
        public void Renders_raster_row_setting_leftmost_dot()
        {
            // Start raster at left margin (ESC*r0A), one row 0x80 = only the leftmost dot set.
            var pcl = Bytes(Esc, "E", Esc, "*t96R", Esc, "*r0A", Esc, "*b1W", new byte[] { 0x80 }, Esc, "*rB");
            var opt = new PclRenderOptions { Margin = 0, Antialias = false };
            using (var bmp = PclRenderer.RenderToBitmap(pcl, opt))
            {
                Assert.Equal(Color.Black.ToArgb(), bmp.GetPixel(0, 0).ToArgb());   // leftmost dot inked
                Assert.NotEqual(Color.Black.ToArgb(), bmp.GetPixel(1, 0).ToArgb()); // next dot clear
            }
        }

        [Fact]
        public void Renders_to_png_bytes()
        {
            var png = PclRenderer.RenderToPng(Bytes(Esc, "E", "hello"));
            Assert.True(png.Length > 8);
            Assert.Equal(0x89, png[0]);   // PNG signature
            Assert.Equal((byte)'P', png[1]);
        }

        private static bool HasNonWhitePixel(Bitmap bmp)
        {
            int white = Color.White.ToArgb();
            for (int y = 0; y < bmp.Height; y++)
                for (int x = 0; x < bmp.Width; x++)
                    if (bmp.GetPixel(x, y).ToArgb() != white) return true;
            return false;
        }
    }
}
