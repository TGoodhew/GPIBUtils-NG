using System;
using System.Drawing;
using System.Linq;
using System.Text;
using GpibUtils.Hardcopy;
using GpibUtils.Instruments.Plotters;
using GpibUtils.Instruments.Printers;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Hardcopy.Tests
{
    public class HardcopyTests
    {
        private static readonly Encoding Latin1 = Encoding.GetEncoding("ISO-8859-1");
        private const string SampleHpgl = "IN;SP1;PU100,100;PD200,200;PU;";

        private static IInstrumentSession Open(string resource)
        {
            var provider = new SimulatedGpibProvider();
            provider.Add(resource, new SimulatedInstrument { IdentificationString = "SIM" });
            return provider.Open(resource);
        }

        // ---- documents ----

        [Fact]
        public void Hpgl_and_pcl_documents_render_to_bitmaps()
        {
            using (var a = new HpglDocument(SampleHpgl).Render())
                Assert.True(a.Width > 0 && a.Height > 0);

            var pcl = Latin1.GetBytes("Ehi");   // ESC E + text
            using (var b = new PclDocument(pcl).Render())
                Assert.True(b.Width > 0 && b.Height > 0);
        }

        // ---- plotter target ----

        [Fact]
        public void Plotter_target_streams_hpgl_natively()
        {
            using (var s = Open(HpPlotter.DefaultResource))
            {
                var plotter = new HpPlotter(s, HpPlotterModel.Hp7475A);
                new PlotterTarget(plotter).Send(new HpglDocument(SampleHpgl));
                Assert.Contains(plotter.History, h => h.StartsWith("PD") || h.StartsWith("PA") || h.Contains("PD"));
            }
        }

        [Fact]
        public void Plotter_target_rejects_non_hpgl()
        {
            using (var s = Open(HpPlotter.DefaultResource))
            {
                var target = new PlotterTarget(new HpPlotter(s, HpPlotterModel.Hp7475A));
                using (var bmp = new Bitmap(4, 4))
                    Assert.Throws<NotSupportedException>(() => target.Send(new ImageDocument(bmp)));
            }
        }

        // ---- thinkjet target ----

        [Fact]
        public void Thinkjet_target_sends_native_pcl_verbatim()
        {
            using (var s = Open(Hp2225A.DefaultResource))
            {
                var printer = new Hp2225A(s);
                var pcl = Latin1.GetBytes("*t96R*r0A*rB");
                new ThinkJetTarget(printer).Send(new PclDocument(pcl));
                var sent = Latin1.GetString(printer.Sent.ToArray());
                Assert.Contains("*t96R*r0A*rB", sent);   // the native PCL passed straight through
            }
        }

        [Fact]
        public void Thinkjet_target_rasterizes_hpgl_to_pcl_raster()
        {
            using (var s = Open(Hp2225A.DefaultResource))
            {
                var printer = new Hp2225A(s);
                new ThinkJetTarget(printer).Send(new HpglDocument(SampleHpgl));
                var sent = Latin1.GetString(printer.Sent.ToArray());
                Assert.Contains("*r0A", sent);   // HP-GL was rasterized and sent as raster graphics
                Assert.Contains("*rB", sent);
            }
        }

        // ---- windows printer target (no physical print) ----

        [Fact]
        public void Windows_printer_target_builds_a_print_document_for_a_named_printer()
        {
            var target = new WindowsPrinterTarget("Microsoft Print to PDF");
            using (var bmp = new Bitmap(20, 10))
            using (var doc = target.BuildPrintDocument(bmp))
            {
                Assert.Equal("Microsoft Print to PDF", doc.PrinterSettings.PrinterName);
                Assert.Equal("GPIBUtils hardcopy", doc.DocumentName);
            }
            Assert.Equal("winprinter", target.Name);
        }

        [Fact]
        public void Windows_printer_target_defaults_to_the_default_printer()
        {
            var target = new WindowsPrinterTarget();
            using (var bmp = new Bitmap(8, 8))
            using (var doc = target.BuildPrintDocument(bmp))
                Assert.NotNull(doc.PrinterSettings);   // no explicit name -> whatever the OS default is
        }
    }
}
