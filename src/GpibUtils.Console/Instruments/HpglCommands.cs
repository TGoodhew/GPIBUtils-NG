using System;
using System.ComponentModel;
using System.IO;
using GpibUtils.Hpgl;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>
    /// Renders a captured HP-GL/2 plot file (e.g. a spectrum-analyzer screen dump pulled over GPIB) to a PNG
    /// or SVG image, using the shared <see cref="GpibUtils.Hpgl"/> renderer (issue #42). A file utility — no
    /// instrument session.
    /// </summary>
    public sealed class HpglRenderCommand : Command<HpglRenderCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "<input>")]
            [Description("Path to the HP-GL/2 plot file (.plt / .hpgl) to render.")]
            public string Input { get; set; }

            [CommandOption("-o|--output <PATH>")]
            [Description("Output image path. Defaults to the input path with a .png (or .svg) extension.")]
            public string Output { get; set; }

            [CommandOption("--svg")]
            [Description("Render to SVG (vector) instead of PNG (raster).")]
            public bool Svg { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) => Runner.Guard(() =>
        {
            if (!File.Exists(settings.Input))
            {
                AnsiConsole.MarkupLineInterpolated($"[red]Input file not found:[/] {settings.Input}");
                return 1;
            }

            byte[] hpgl = File.ReadAllBytes(settings.Input);
            string output = settings.Output ??
                Path.ChangeExtension(settings.Input, settings.Svg ? ".svg" : ".png");

            if (settings.Svg)
                File.WriteAllText(output, HpglRenderer.RenderToSvg(hpgl));
            else
                File.WriteAllBytes(output, HpglRenderer.RenderToPng(hpgl));

            AnsiConsole.MarkupLineInterpolated($"[green]Rendered[/] {settings.Input} [grey]->[/] {output}");
            return 0;
        });
    }
}
