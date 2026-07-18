using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using GpibUtils.Hardcopy;
using GpibUtils.Hpgl;
using GpibUtils.Instruments.Plotters;
using GpibUtils.Instruments.Printers;
using GpibUtils.Pcl;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>Shared input options for the hardcopy commands: exactly one of --hpgl / --pcl / --image.</summary>
    public abstract class HardcopyInputSettings : InstrumentSettings
    {
        [CommandOption("--hpgl <FILE>")] [Description("HP-GL document file.")] public string HpglFile { get; set; }
        [CommandOption("--pcl <FILE>")] [Description("PCL document file.")] public string PclFile { get; set; }
        [CommandOption("--image <FILE>")] [Description("Image file (PNG/BMP/...).")] public string ImageFile { get; set; }

        internal HardcopyDocument LoadDocument()
        {
            int n = (HpglFile != null ? 1 : 0) + (PclFile != null ? 1 : 0) + (ImageFile != null ? 1 : 0);
            if (n != 1) throw new ArgumentException("Specify exactly one of --hpgl, --pcl or --image.");
            if (HpglFile != null) return new HpglDocument(File.ReadAllText(HpglFile));
            if (PclFile != null) return new PclDocument(File.ReadAllBytes(PclFile));
            return new ImageDocument(new Bitmap(Image.FromFile(ImageFile)));
        }
    }

    /// <summary>Render an HP-GL or PCL document to a PNG preview (no hardware).</summary>
    public sealed class HardcopyPreviewCommand : Command<HardcopyPreviewCommand.Settings>
    {
        public sealed class Settings : HardcopyInputSettings
        {
            [CommandOption("-o|--out <FILE>")]
            [Description("Output PNG path (default hardcopy.png).")]
            public string Out { get; set; }
        }

        public override int Execute(CommandContext context, Settings s) => Runner.Guard(() =>
        {
            var outPath = string.IsNullOrWhiteSpace(s.Out) ? "hardcopy.png" : s.Out;
            byte[] png;
            if (s.HpglFile != null) png = HpglRenderer.RenderToPng(File.ReadAllText(s.HpglFile),
                                            new HpglRenderOptions { Background = HpglBackground.White });
            else if (s.PclFile != null) png = PclRenderer.RenderToPng(File.ReadAllBytes(s.PclFile));
            else using (var bmp = s.LoadDocument().Render())
                using (var ms = new MemoryStream()) { bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png); png = ms.ToArray(); }

            File.WriteAllBytes(outPath, png);
            AnsiConsole.MarkupLineInterpolated($"[green]wrote {png.Length} bytes to {outPath}[/]");
            return 0;
        });
    }

    /// <summary>Send an HP-GL/PCL/image document to a plotter, the ThinkJet, or a Windows printer.</summary>
    public sealed class HardcopySendCommand : Command<HardcopySendCommand.Settings>
    {
        public sealed class Settings : HardcopyInputSettings
        {
            [CommandOption("--to <TARGET>")]
            [Description("plotter | thinkjet | winprinter[:<printer name>].")]
            public string To { get; set; }
        }

        public override int Execute(CommandContext context, Settings s) => Runner.Guard(() =>
        {
            var doc = s.LoadDocument();
            var to = (s.To ?? string.Empty).Trim();
            string kind = to, arg = null;
            int colon = to.IndexOf(':');
            if (colon >= 0) { kind = to.Substring(0, colon); arg = to.Substring(colon + 1); }

            switch (kind.ToLowerInvariant())
            {
                case "plotter":
                {
                    var session = s.OpenSession("plotter", HpPlotter.DefaultResource);
                    using (session) new PlotterTarget(new HpPlotter(session)).Send(doc);
                    break;
                }
                case "thinkjet":
                {
                    var session = s.OpenSession("thinkjet", Hp2225A.DefaultResource);
                    using (session) new ThinkJetTarget(new Hp2225A(session)).Send(doc);
                    break;
                }
                case "winprinter":
                    new WindowsPrinterTarget(arg).Send(doc);
                    break;
                default:
                    throw new ArgumentException($"Unknown --to target '{s.To}'. Use plotter | thinkjet | winprinter[:name].");
            }
            AnsiConsole.MarkupLineInterpolated($"[green]sent to {kind}[/]");
            return 0;
        });
    }
}
