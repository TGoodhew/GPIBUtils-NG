using System;
using System.Collections.ObjectModel;
using System.Linq;
using GpibUtils.Visa;
using GpibUtils.Wpf.Mvvm;

namespace GpibUtils.Wpf.ViewModels
{
    /// <summary>
    /// The shell's main view model. It sits on the shared <see cref="GpibProviders"/> registry — the same
    /// core the console front-end uses — to list providers, discover instruments, and run a command.
    /// With the Simulated provider it works with no hardware.
    /// </summary>
    public sealed class MainViewModel : ViewModelBase
    {
        private string _selectedProvider;
        private string _resource = "GPIB0::5::INSTR";
        private string _command = "*IDN?";
        private string _reply = string.Empty;
        private string _status = "Ready.";
        private int _timeoutMs = 5000;

        public MainViewModel()
        {
            DiscoverCommand = new RelayCommand(Discover, () => !string.IsNullOrWhiteSpace(SelectedProvider));
            QueryCommand = new RelayCommand(Query,
                () => !string.IsNullOrWhiteSpace(SelectedProvider) && !string.IsNullOrWhiteSpace(Resource));
            RefreshProvidersCommand = new RelayCommand(LoadProviders);
            LoadProviders();
        }

        public ObservableCollection<ProviderRow> Providers { get; } = new ObservableCollection<ProviderRow>();
        public ObservableCollection<string> ProviderNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Discovered { get; } = new ObservableCollection<string>();

        public RelayCommand DiscoverCommand { get; }
        public RelayCommand QueryCommand { get; }
        public RelayCommand RefreshProvidersCommand { get; }

        public string SelectedProvider
        {
            get => _selectedProvider;
            set => SetProperty(ref _selectedProvider, value);
        }

        public string Resource
        {
            get => _resource;
            set => SetProperty(ref _resource, value);
        }

        public string Command
        {
            get => _command;
            set => SetProperty(ref _command, value);
        }

        public int TimeoutMs
        {
            get => _timeoutMs;
            set => SetProperty(ref _timeoutMs, value);
        }

        public string Reply
        {
            get => _reply;
            private set => SetProperty(ref _reply, value);
        }

        public string Status
        {
            get => _status;
            private set => SetProperty(ref _status, value);
        }

        private void LoadProviders()
        {
            Providers.Clear();
            ProviderNames.Clear();

            var defaultName = GpibProviders.DefaultProviderName;
            foreach (var p in GpibProviders.All.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                Providers.Add(new ProviderRow(p, string.Equals(p.Name, defaultName, StringComparison.OrdinalIgnoreCase)));
                ProviderNames.Add(p.Name);
            }

            if (string.IsNullOrWhiteSpace(SelectedProvider) || !ProviderNames.Contains(SelectedProvider))
                SelectedProvider = ProviderNames.Contains(defaultName) ? defaultName : ProviderNames.FirstOrDefault();

            Status = $"{Providers.Count} provider(s) registered.";
        }

        private void Discover()
        {
            Discovered.Clear();
            Guard(() =>
            {
                var provider = GpibProviders.Get(SelectedProvider);
                var found = provider.Discover();
                foreach (var r in found) Discovered.Add(r);
                if (found.Count == 0)
                    Status = $"No instruments found via {provider.Name}.";
                else if (found.Count >= 15)
                    Status = $"{found.Count} present via {provider.Name} — nearly the whole bus, so an " +
                             "HP-IB extender (HP 37204A or similar) is in the path. This list is phantom; " +
                             "drive instruments by explicit address.";
                else
                    Status = $"{found.Count} instrument(s) via {provider.Name}. " +
                             "Note: bus extenders can make addresses appear present — prefer explicit addresses.";
            });
        }

        private void Query()
        {
            Guard(() =>
            {
                using (var session = GpibProviders.Open(SelectedProvider, Resource,
                    new SessionSettings { TimeoutMilliseconds = TimeoutMs }))
                {
                    Reply = session.Query(Command);
                    Status = $"Sent to {Resource} via {SelectedProvider}.";
                }
            });
        }

        private void Guard(Action action)
        {
            try { action(); }
            catch (Exception ex) { Status = "Error: " + ex.Message; }
        }
    }
}
