using System;
using System.Drawing;
using GpibUtils.Hpgl;
using GpibUtils.Pcl;

namespace GpibUtils.Hardcopy
{
    /// <summary>
    /// A page-description document that can be rasterized to a <see cref="Bitmap"/> for any raster sink
    /// (on-screen preview, a Windows printer, or the ThinkJet's raster mode). Concrete kinds wrap HP-GL, PCL,
    /// or a ready-made image. The common raster is what makes the sinks interchangeable — the same plot can go
    /// to a plotter (native vector), the ThinkJet, a Windows printer, or a preview pane.
    /// </summary>
    public abstract class HardcopyDocument
    {
        /// <summary>Renders the document to a fresh <see cref="Bitmap"/> on white paper (caller disposes).</summary>
        public abstract Bitmap Render();
    }

    /// <summary>An HP-GL document — rendered to raster via <c>GpibUtils.Hpgl</c>, or streamed natively to a plotter.</summary>
    public sealed class HpglDocument : HardcopyDocument
    {
        public string Hpgl { get; }
        public HpglRenderOptions Options { get; }

        public HpglDocument(string hpgl, HpglRenderOptions options = null)
        {
            Hpgl = hpgl ?? string.Empty;
            // Default to white-paper rendering — hardcopy is for printing, not a CRT.
            Options = options ?? new HpglRenderOptions { Background = HpglBackground.White };
        }

        public override Bitmap Render() => HpglRenderer.RenderToBitmap(Hpgl, Options);
    }

    /// <summary>A PCL document — rendered to raster via <c>GpibUtils.Pcl</c>, or streamed natively to the ThinkJet.</summary>
    public sealed class PclDocument : HardcopyDocument
    {
        public byte[] Pcl { get; }
        public PclRenderOptions Options { get; }

        public PclDocument(byte[] pcl, PclRenderOptions options = null)
        {
            Pcl = pcl ?? new byte[0];
            Options = options ?? new PclRenderOptions();
        }

        public override Bitmap Render() => PclRenderer.RenderToBitmap(Pcl, Options);
    }

    /// <summary>A ready-made bitmap document (e.g. an instrument screen capture).</summary>
    public sealed class ImageDocument : HardcopyDocument
    {
        private readonly Bitmap _image;
        public ImageDocument(Bitmap image) => _image = image ?? throw new ArgumentNullException(nameof(image));
        public override Bitmap Render() => (Bitmap)_image.Clone();
    }
}
