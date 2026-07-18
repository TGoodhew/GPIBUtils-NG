using System.Drawing;
using System.Linq;
using System.Text;
using GpibUtils.Instruments.Printers;
using GpibUtils.Pcl;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.Printers.Tests
{
    public class Hp2225ATests
    {
        private static readonly Encoding Latin1 = Encoding.GetEncoding("ISO-8859-1");

        private static IInstrumentSession Open()
        {
            var provider = new SimulatedGpibProvider();
            provider.Add(Hp2225A.DefaultResource, new SimulatedInstrument { IdentificationString = "THINKJET" });
            return provider.Open(Hp2225A.DefaultResource);
        }

        [Fact]
        public void Implements_printer_and_default_address_1()
        {
            using (var s = Open())
            {
                var d = new Hp2225A(s);
                Assert.IsAssignableFrom<IPrinter>(d);
                Assert.IsAssignableFrom<IHardcopyDevice>(d);
                Assert.Equal("GPIB0::1::INSTR", Hp2225A.DefaultResource);
                Assert.Contains("ThinkJet", d.Identify());
            }
        }

        [Fact]
        public void Initialize_sends_reset()
        {
            using (var s = Open())
            {
                var d = new Hp2225A(s);
                d.Initialize();
                Assert.Equal(Latin1.GetBytes("E"), d.Sent.ToArray());
            }
        }

        [Fact]
        public void Print_text_normalizes_newlines_to_crlf()
        {
            using (var s = Open())
            {
                var d = new Hp2225A(s);
                d.PrintText("A\nB");
                Assert.Equal(Latin1.GetBytes("A\r\nB"), d.Sent.ToArray());
            }
        }

        [Fact]
        public void Set_resolution_emits_pcl()
        {
            using (var s = Open())
            {
                var d = new Hp2225A(s);
                d.SetResolutionDpi(192);
                Assert.Equal(Latin1.GetBytes("*t192R"), d.Sent.ToArray());
            }
        }

        [Fact]
        public void Raster_round_trips_through_the_pcl_renderer()
        {
            // A 2x2 checkerboard: (0,0) and (1,1) black; the others white.
            using (var bmp = new Bitmap(2, 2))
            {
                bmp.SetPixel(0, 0, Color.Black); bmp.SetPixel(1, 0, Color.White);
                bmp.SetPixel(0, 1, Color.White); bmp.SetPixel(1, 1, Color.Black);

                using (var s = Open())
                {
                    var d = new Hp2225A(s);
                    d.PrintRaster(bmp);
                    var pcl = d.Sent.ToArray();

                    // The emitted PCL, rendered back, must reproduce the inked pixels.
                    var opt = new PclRenderOptions { Margin = 0, Antialias = false };
                    using (var render = PclRenderer.RenderToBitmap(pcl, opt))
                    {
                        Assert.Equal(Color.Black.ToArgb(), render.GetPixel(0, 0).ToArgb());
                        Assert.NotEqual(Color.Black.ToArgb(), render.GetPixel(1, 0).ToArgb());
                        Assert.NotEqual(Color.Black.ToArgb(), render.GetPixel(0, 1).ToArgb());
                        Assert.Equal(Color.Black.ToArgb(), render.GetPixel(1, 1).ToArgb());
                    }
                }
            }
        }

        [Fact]
        public void Raster_envelope_has_start_and_end_markers()
        {
            using (var bmp = new Bitmap(8, 1))
            using (var s = Open())
            {
                var d = new Hp2225A(s);
                d.PrintRaster(bmp);
                var pcl = Latin1.GetString(d.Sent.ToArray());
                Assert.Contains("*r0A", pcl);   // start raster
                Assert.Contains("*b1W", pcl);   // one byte/row for 8 px
                Assert.Contains("*rB", pcl);    // end raster
            }
        }
    }
}
