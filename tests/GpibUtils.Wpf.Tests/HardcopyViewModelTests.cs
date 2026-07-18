using System;
using System.IO;
using GpibUtils.Hardcopy;
using GpibUtils.Wpf.ViewModels;
using Xunit;

namespace GpibUtils.Wpf.Tests
{
    public class HardcopyViewModelTests
    {
        private const string SampleHpgl = "IN;SP1;PU100,100;PD900,900;PU;";

        private static string TempFile(string ext, byte[] content)
        {
            var path = Path.Combine(Path.GetTempPath(), "hc_" + Guid.NewGuid().ToString("N") + ext);
            File.WriteAllBytes(path, content);
            return path;
        }

        [Fact]
        public void Populates_type_target_and_provider_lists()
        {
            var vm = new HardcopyViewModel();
            Assert.Contains("HP-GL", vm.DocumentTypes);
            Assert.Contains("PCL", vm.DocumentTypes);
            Assert.Contains(HardcopyViewModel.ThinkJet, vm.Targets);
            Assert.Contains(HardcopyViewModel.Plotter, vm.Targets);
            Assert.Contains(HardcopyViewModel.WindowsPrinter, vm.Targets);
            Assert.Equal(HardcopyViewModel.ThinkJet, vm.SelectedTarget);
        }

        [Theory]
        [InlineData("plot.plt", "HP-GL")]
        [InlineData("out.pcl", "PCL")]
        [InlineData("scan.png", "Image")]
        public void Resolves_type_from_extension_in_auto_mode(string name, string expected)
        {
            var vm = new HardcopyViewModel { InputPath = name, DocumentType = "Auto" };
            Assert.Equal(expected, vm.ResolveType());
        }

        [Fact]
        public void Explicit_type_overrides_extension()
        {
            var vm = new HardcopyViewModel { InputPath = "mystery.dat", DocumentType = "PCL" };
            Assert.Equal("PCL", vm.ResolveType());
        }

        [Fact]
        public void Loads_hpgl_and_pcl_documents_by_type()
        {
            var hpgl = TempFile(".plt", System.Text.Encoding.ASCII.GetBytes(SampleHpgl));
            var pcl = TempFile(".pcl", new byte[] { 0x1B, (byte)'E', (byte)'h', (byte)'i' });
            try
            {
                Assert.IsType<HpglDocument>(new HardcopyViewModel { InputPath = hpgl }.LoadDocument());
                Assert.IsType<PclDocument>(new HardcopyViewModel { InputPath = pcl }.LoadDocument());
            }
            finally { File.Delete(hpgl); File.Delete(pcl); }
        }

        [Fact]
        public void Renders_preview_png_for_hpgl()
        {
            var hpgl = TempFile(".plt", System.Text.Encoding.ASCII.GetBytes(SampleHpgl));
            try
            {
                var png = new HardcopyViewModel { InputPath = hpgl }.RenderPreviewPng();
                Assert.True(png.Length > 8);
                Assert.Equal(0x89, png[0]);   // PNG signature
                Assert.Equal((byte)'P', png[1]);
            }
            finally { File.Delete(hpgl); }
        }
    }
}
