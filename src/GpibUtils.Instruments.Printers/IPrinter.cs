using System.Drawing;

namespace GpibUtils.Instruments.Printers
{
    /// <summary>
    /// A bus-connected hardcopy output device — the shared surface of a printer or plotter driven over
    /// <see cref="Visa.IInstrumentSession"/>. Content-agnostic: it identifies, initializes and ejects a page.
    /// (Plotters live in <c>GpibUtils.Instruments.Plotters</c> and are unified with printers at the hardcopy
    /// routing layer, so this base stays dependency-light.)
    /// </summary>
    public interface IHardcopyDevice
    {
        /// <summary>The resource string this device's session was opened for.</summary>
        string ResourceName { get; }

        /// <summary>Identifies the device (returns a descriptor for devices with no query).</summary>
        string Identify();

        /// <summary>Device clear + reset to a known state.</summary>
        void Initialize();

        /// <summary>Ejects / advances to the next page (form feed).</summary>
        void FormFeed();
    }

    /// <summary>
    /// A raster/text printer (HP 2225 ThinkJet and compatible HP-IB printers). Accepts plain text laid out on
    /// the printer's character grid and PCL raster graphics — the counterpart to a vector HP-GL plotter.
    /// Because a rasterized page is just a <see cref="Bitmap"/>, anything the plotters can draw (via
    /// <c>HpglRenderer.RenderToBitmap</c>) can be printed here too. New interface for issue #166.
    /// </summary>
    public interface IPrinter : IHardcopyDevice
    {
        /// <summary>Sets the raster resolution, in dots per inch (<c>ESC*t&lt;dpi&gt;R</c>).</summary>
        void SetResolutionDpi(int dpi);

        /// <summary>Prints plain text at the current position (newlines become CR+LF).</summary>
        void PrintText(string text);

        /// <summary>Prints a bitmap as PCL raster graphics (1-bpp; a pixel is inked when it is dark).</summary>
        void PrintRaster(Bitmap image);
    }
}
