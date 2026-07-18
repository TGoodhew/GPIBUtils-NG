using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using GpibUtils.Hardcopy;
using GpibUtils.Instruments.Plotters;
using GpibUtils.Instruments.Printers;
using GpibUtils.Visa;
using GpibUtils.Wpf.Mvvm;

namespace GpibUtils.Wpf.ViewModels
{
    /// <summary>
    /// The Hardcopy tab: load an HP-GL / PCL / image file, preview it rendered to a raster, and route it to a
    /// GPIB plotter, the GPIB ThinkJet, or a normal Windows printer — the same
    /// <see cref="GpibUtils.Hardcopy"/> layer the CLI uses. Rendering works with no hardware; plotter/ThinkJet
    /// sends open a session through the shared <see cref="GpibProviders"/> registry (Simulated needs none).
    /// </summary>
    public sealed class HardcopyViewModel : ViewModelBase
    {
        public const string Plotter = "Plotter (GPIB)";
        public const string ThinkJet = "ThinkJet (GPIB)";
        public const string WindowsPrinter = "Windows printer";

        private string _inputPath = string.Empty;
        private string _documentType = "Auto";
        private string _selectedTarget = ThinkJet;
        private string _printerName = string.Empty;
        private string _selectedProvider;
        private string _status = "Load an HP-GL, PCL, or image file.";
        private BitmapSource _preview;

        public HardcopyViewModel()
        {
            foreach (var t in new[] { "Auto", "HP-GL", "PCL", "Image" }) DocumentTypes.Add(t);
            foreach (var t in new[] { Plotter, ThinkJet, WindowsPrinter }) Targets.Add(t);
            foreach (var p in GpibProviders.All.Select(x => x.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
                ProviderNames.Add(p);
            _selectedProvider = GpibProviders.DefaultProviderName ?? ProviderNames.FirstOrDefault();

            BrowseCommand = new RelayCommand(Browse);
            PreviewCommand = new RelayCommand(DoPreview, () => File.Exists(InputPath));
            SendCommand = new RelayCommand(DoSend, () => File.Exists(InputPath));
        }

        public ObservableCollection<string> DocumentTypes { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Targets { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> ProviderNames { get; } = new ObservableCollection<string>();

        public RelayCommand BrowseCommand { get; }
        public RelayCommand PreviewCommand { get; }
        public RelayCommand SendCommand { get; }

        public string InputPath { get => _inputPath; set => SetProperty(ref _inputPath, value); }
        public string DocumentType { get => _documentType; set => SetProperty(ref _documentType, value); }
        public string SelectedTarget { get => _selectedTarget; set => SetProperty(ref _selectedTarget, value); }
        public string PrinterName { get => _printerName; set => SetProperty(ref _printerName, value); }
        public string SelectedProvider { get => _selectedProvider; set => SetProperty(ref _selectedProvider, value); }
        public string Status { get => _status; private set => SetProperty(ref _status, value); }
        public BitmapSource Preview { get => _preview; private set => SetProperty(ref _preview, value); }

        // --- testable core (no UI/dispatcher) --------------------------------

        /// <summary>Resolves the effective document type from <see cref="DocumentType"/> or the file extension.</summary>
        internal string ResolveType()
        {
            if (!string.Equals(DocumentType, "Auto", StringComparison.OrdinalIgnoreCase)) return DocumentType;
            var ext = (Path.GetExtension(InputPath) ?? string.Empty).ToLowerInvariant();
            if (ext == ".hpgl" || ext == ".plt" || ext == ".hgl") return "HP-GL";
            if (ext == ".pcl" || ext == ".prn") return "PCL";
            return "Image";
        }

        /// <summary>Loads the current file as a routable <see cref="HardcopyDocument"/>.</summary>
        internal HardcopyDocument LoadDocument()
        {
            switch (ResolveType())
            {
                case "HP-GL": return new HpglDocument(File.ReadAllText(InputPath));
                case "PCL": return new PclDocument(File.ReadAllBytes(InputPath));
                default: return new ImageDocument(new Bitmap(System.Drawing.Image.FromFile(InputPath)));
            }
        }

        /// <summary>Renders the current document to PNG bytes (for the preview and for tests).</summary>
        internal byte[] RenderPreviewPng()
        {
            using (var bmp = LoadDocument().Render())
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                return ms.ToArray();
            }
        }

        /// <summary>Routes the current document to the selected target, opening a session where needed.</summary>
        internal void SendToTarget()
        {
            var doc = LoadDocument();
            switch (SelectedTarget)
            {
                case Plotter:
                    using (var s = GpibProviders.Open(SelectedProvider, HpPlotter.DefaultResource, new SessionSettings()))
                        new PlotterTarget(new HpPlotter(s)).Send(doc);
                    break;
                case ThinkJet:
                    using (var s = GpibProviders.Open(SelectedProvider, Hp2225A.DefaultResource, new SessionSettings()))
                        new ThinkJetTarget(new Hp2225A(s)).Send(doc);
                    break;
                default:
                    new WindowsPrinterTarget(string.IsNullOrWhiteSpace(PrinterName) ? null : PrinterName).Send(doc);
                    break;
            }
        }

        // --- commands --------------------------------------------------------

        private void Browse()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Hardcopy (*.hpgl;*.plt;*.pcl;*.prn;*.png;*.bmp;*.jpg)|*.hpgl;*.plt;*.pcl;*.prn;*.png;*.bmp;*.jpg|All files (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true) { InputPath = dlg.FileName; DoPreview(); }
        }

        private void DoPreview()
        {
            try { Preview = ToImage(RenderPreviewPng()); Status = $"Previewed {ResolveType()}: {Path.GetFileName(InputPath)}."; }
            catch (Exception ex) { Status = "Preview failed: " + ex.Message; }
        }

        private void DoSend()
        {
            try { SendToTarget(); Status = $"Sent {Path.GetFileName(InputPath)} to {SelectedTarget}."; }
            catch (Exception ex) { Status = "Send failed: " + ex.Message; }
        }

        private static BitmapSource ToImage(byte[] png)
        {
            using (var ms = new MemoryStream(png))
            {
                var img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.StreamSource = ms;
                img.EndInit();
                img.Freeze();
                return img;
            }
        }
    }
}
