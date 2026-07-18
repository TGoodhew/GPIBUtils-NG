using System;
using System.Drawing;
using System.Drawing.Printing;
using GpibUtils.Instruments.Plotters;
using GpibUtils.Instruments.Printers;

namespace GpibUtils.Hardcopy
{
    /// <summary>A destination a <see cref="HardcopyDocument"/> can be sent to.</summary>
    public interface IHardcopyTarget
    {
        /// <summary>Short target name (e.g. "plotter", "thinkjet", "winprinter").</summary>
        string Name { get; }

        /// <summary>Sends the document to this target, rendering to raster if the target needs it.</summary>
        void Send(HardcopyDocument document);
    }

    /// <summary>A GPIB HP-GL plotter sink. Streams HP-GL natively; other document kinds can't be drawn
    /// vector-wise on a pen plotter, so they are rejected (rasterize and use the ThinkJet or a Windows printer
    /// instead).</summary>
    public sealed class PlotterTarget : IHardcopyTarget
    {
        private readonly IPlotter _plotter;
        public PlotterTarget(IPlotter plotter) => _plotter = plotter ?? throw new ArgumentNullException(nameof(plotter));
        public string Name => "plotter";

        public void Send(HardcopyDocument document)
        {
            if (document is HpglDocument h) { _plotter.Initialize(); _plotter.PlotHpgl(h.Hpgl); }
            else throw new NotSupportedException(
                "A pen plotter draws HP-GL vectors only; rasterize the document and send it to the ThinkJet or a Windows printer.");
        }
    }

    /// <summary>The GPIB HP 2225 ThinkJet sink. Streams a native PCL document verbatim; anything else is
    /// rasterized and printed as PCL raster graphics.</summary>
    public sealed class ThinkJetTarget : IHardcopyTarget
    {
        private readonly Hp2225A _printer;
        public ThinkJetTarget(Hp2225A printer) => _printer = printer ?? throw new ArgumentNullException(nameof(printer));
        public string Name => "thinkjet";

        public void Send(HardcopyDocument document)
        {
            _printer.Initialize();
            if (document is PclDocument p) { _printer.SendRaw(p.Pcl); }
            else using (var bmp = document.Render()) _printer.PrintRaster(bmp);
            _printer.FormFeed();
        }
    }

    /// <summary>A normal Windows printer sink (local or network) via <see cref="PrintDocument"/>. Renders the
    /// document to a bitmap and prints it fit-to-page. Uses the default printer when no name is given.</summary>
    public sealed class WindowsPrinterTarget : IHardcopyTarget
    {
        private readonly string _printerName;   // null / empty => default printer
        public WindowsPrinterTarget(string printerName = null) => _printerName = printerName;
        public string Name => "winprinter";

        public void Send(HardcopyDocument document)
        {
            using (var bmp = document.Render())
            using (var doc = BuildPrintDocument(bmp))
                doc.Print();
        }

        /// <summary>Builds (but does not run) the <see cref="PrintDocument"/> that would print the bitmap
        /// fit-to-page. Exposed so the routing can be exercised without a physical printer.</summary>
        internal PrintDocument BuildPrintDocument(Bitmap bitmap)
        {
            if (bitmap == null) throw new ArgumentNullException(nameof(bitmap));
            var doc = new PrintDocument { DocumentName = "GPIBUtils hardcopy" };
            if (!string.IsNullOrWhiteSpace(_printerName)) doc.PrinterSettings.PrinterName = _printerName;

            doc.PrintPage += (s, e) =>
            {
                var area = e.MarginBounds;
                double scale = Math.Min((double)area.Width / bitmap.Width, (double)area.Height / bitmap.Height);
                int w = (int)(bitmap.Width * scale), h = (int)(bitmap.Height * scale);
                int x = area.Left + (area.Width - w) / 2, y = area.Top + (area.Height - h) / 2;
                e.Graphics.DrawImage(bitmap, new Rectangle(x, y, w, h));
                e.HasMorePages = false;
            };
            return doc;
        }
    }
}
