using System.Drawing;

namespace GpibUtils.Pcl
{
    /// <summary>Background fill for the rendered page.</summary>
    public enum PclBackground
    {
        /// <summary>White paper (the natural look for a printer; default).</summary>
        White,
        /// <summary>Black canvas (for on-screen viewing alongside instrument captures).</summary>
        Black
    }

    /// <summary>
    /// Options controlling how a PCL stream is rasterized by <see cref="PclRenderer"/>. Defaults render an
    /// HP 2225 ThinkJet page: 96 dpi, ~6.4 in printable width (uses the stream's own <c>ESC*t…R</c>
    /// resolution when present), white paper, black ink, auto-height to fit the content.
    /// </summary>
    public sealed class PclRenderOptions
    {
        /// <summary>Device resolution in dots per inch when the stream does not set one (default 96 — the
        /// ThinkJet's native raster resolution).</summary>
        public int DefaultDpi { get; set; } = 96;

        /// <summary>Printable page width in dots (default 640 = 6.67 in at 96 dpi, the ThinkJet raster width).</summary>
        public int PageWidthDots { get; set; } = 640;

        /// <summary>Maximum page height in dots the renderer will grow to (default 4096 — a safety cap).</summary>
        public int MaxHeightDots { get; set; } = 4096;

        /// <summary>Border (device dots) kept clear around the page content (default 8).</summary>
        public int Margin { get; set; } = 8;

        /// <summary>Page background (default <see cref="PclBackground.White"/>).</summary>
        public PclBackground Background { get; set; } = PclBackground.White;

        /// <summary>Ink colour for text and set raster dots (default black).</summary>
        public Color Ink { get; set; } = Color.Black;

        /// <summary>Antialias text (default true). Raster dots are always drawn crisp.</summary>
        public bool Antialias { get; set; } = true;

        internal Color ResolveBackground() => Background == PclBackground.Black ? Color.Black : Color.White;

        internal Color ResolveInk() =>
            Background == PclBackground.Black && Ink == Color.Black ? Color.White : Ink;
    }
}
