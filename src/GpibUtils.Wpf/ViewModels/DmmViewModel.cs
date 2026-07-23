using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Threading;
using GpibUtils.Common;
using GpibUtils.Instruments.Meters;
using GpibUtils.Visa;
using GpibUtils.Wpf.Mvvm;

namespace GpibUtils.Wpf.ViewModels
{
    /// <summary>
    /// The DMM tab: drive an HP/Agilent/Keysight 34401A over the shared core — configure a function/range/
    /// NPLC, take a single reading or a burst with statistics, and run a live monitor that polls on a
    /// <see cref="DispatcherTimer"/> and shows the value + running min/max/avg/sd. This is the WPF peer of
    /// the CLI <c>hp34401a measure/stats/monitor</c> verbs and the TUI DMM panel (issue #172) — the same
    /// DMM capability in all three front-ends (UI parity). Works with no hardware via the Simulated provider.
    /// </summary>
    public sealed class DmmViewModel : ViewModelBase
    {
        private static readonly Dictionary<string, MeasurementFunction> FunctionMap =
            new Dictionary<string, MeasurementFunction>(StringComparer.OrdinalIgnoreCase)
            {
                ["dcv"] = MeasurementFunction.DcVoltage,
                ["acv"] = MeasurementFunction.AcVoltage,
                ["dci"] = MeasurementFunction.DcCurrent,
                ["aci"] = MeasurementFunction.AcCurrent,
                ["res"] = MeasurementFunction.Resistance2Wire,
                ["fres"] = MeasurementFunction.Resistance4Wire,
                ["freq"] = MeasurementFunction.Frequency,
                ["per"] = MeasurementFunction.Period,
                ["cont"] = MeasurementFunction.Continuity,
                ["diode"] = MeasurementFunction.Diode,
            };

        private readonly RunningStatistics _running = new RunningStatistics();
        private DispatcherTimer _timer;
        private IInstrumentSession _monitorSession;
        private Hp34401A _monitorDmm;
        private string _monitorUnit = "V";

        private string _selectedProvider;
        private string _resource = Hp34401A.DefaultResource;
        private string _selectedFunction = "dcv";
        private string _range = string.Empty;
        private string _nplc = string.Empty;
        private int _intervalMs = 500;
        private int _burstCount = 100;
        private string _lastValue = "—";
        private string _statistics = string.Empty;
        private string _status = "Configure a function, then Read / Burst / Monitor.";
        private bool _isMonitoring;

        public DmmViewModel()
        {
            foreach (var f in FunctionMap.Keys) Functions.Add(f);
            foreach (var p in GpibProviders.All.Select(x => x.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
                ProviderNames.Add(p);
            _selectedProvider = GpibProviders.DefaultProviderName ?? ProviderNames.FirstOrDefault();

            ReadOnceCommand = new RelayCommand(ReadOnce, () => !IsMonitoring);
            BurstCommand = new RelayCommand(Burst, () => !IsMonitoring);
            StartMonitorCommand = new RelayCommand(StartMonitor, () => !IsMonitoring);
            StopMonitorCommand = new RelayCommand(StopMonitor, () => IsMonitoring);
        }

        public ObservableCollection<string> Functions { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> ProviderNames { get; } = new ObservableCollection<string>();

        public RelayCommand ReadOnceCommand { get; }
        public RelayCommand BurstCommand { get; }
        public RelayCommand StartMonitorCommand { get; }
        public RelayCommand StopMonitorCommand { get; }

        public string SelectedProvider { get => _selectedProvider; set => SetProperty(ref _selectedProvider, value); }
        public string Resource { get => _resource; set => SetProperty(ref _resource, value); }
        public string SelectedFunction { get => _selectedFunction; set => SetProperty(ref _selectedFunction, value); }
        public string Range { get => _range; set => SetProperty(ref _range, value); }
        public string Nplc { get => _nplc; set => SetProperty(ref _nplc, value); }
        public int IntervalMs { get => _intervalMs; set => SetProperty(ref _intervalMs, value); }
        public int BurstCount { get => _burstCount; set => SetProperty(ref _burstCount, value); }
        public string LastValue { get => _lastValue; private set => SetProperty(ref _lastValue, value); }
        public string Statistics { get => _statistics; private set => SetProperty(ref _statistics, value); }
        public string Status { get => _status; private set => SetProperty(ref _status, value); }
        public bool IsMonitoring { get => _isMonitoring; private set => SetProperty(ref _isMonitoring, value); }

        // --- testable core (no dispatcher) -----------------------------------

        /// <summary>Maps the selected friendly function name to a <see cref="MeasurementFunction"/>
        /// (defaults to DC voltage for an unknown name).</summary>
        internal MeasurementFunction ResolveFunction() =>
            FunctionMap.TryGetValue(SelectedFunction ?? string.Empty, out var fn) ? fn : MeasurementFunction.DcVoltage;

        /// <summary>The SI unit for a function, for engineering-format display.</summary>
        internal static string UnitFor(MeasurementFunction fn)
        {
            switch (fn)
            {
                case MeasurementFunction.DcVoltage:
                case MeasurementFunction.AcVoltage:
                case MeasurementFunction.Diode: return "V";
                case MeasurementFunction.DcCurrent:
                case MeasurementFunction.AcCurrent: return "A";
                case MeasurementFunction.Resistance2Wire:
                case MeasurementFunction.Resistance4Wire:
                case MeasurementFunction.Continuity: return "Ω";
                case MeasurementFunction.Frequency: return "Hz";
                case MeasurementFunction.Period: return "s";
                default: return "";
            }
        }

        private Hp34401A OpenDmm(out IInstrumentSession session)
        {
            session = GpibProviders.Open(SelectedProvider, Resource, new SessionSettings { TimeoutMilliseconds = 5000 });
            return new Hp34401A(session);
        }

        private MeasurementFunction ApplyConfig(Hp34401A dmm)
        {
            var fn = ResolveFunction();
            dmm.Configure(fn, string.IsNullOrWhiteSpace(Range) ? null : Range.Trim());
            if (double.TryParse(Nplc, NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
                dmm.SetNplc(fn, n);
            return fn;
        }

        // --- commands --------------------------------------------------------

        private void ReadOnce()
        {
            Guard(() =>
            {
                using (var s = GpibProviders.Open(SelectedProvider, Resource, new SessionSettings { TimeoutMilliseconds = 5000 }))
                {
                    var dmm = new Hp34401A(s);
                    var fn = ApplyConfig(dmm);
                    double v = dmm.ReadValue();
                    LastValue = ToEngineeringFormat.Convert(v, 6, UnitFor(fn));
                    Statistics = "single read";
                    Status = $"Read {Resource} via {SelectedProvider}.";
                }
            });
        }

        private void Burst()
        {
            Guard(() =>
            {
                using (var s = GpibProviders.Open(SelectedProvider, Resource, new SessionSettings { TimeoutMilliseconds = 5000 }))
                {
                    var dmm = new Hp34401A(s);
                    var fn = ApplyConfig(dmm);
                    var values = dmm.ReadValues(BurstCount);
                    var stat = DmmStatistics.Of(values);
                    var unit = UnitFor(fn);
                    LastValue = ToEngineeringFormat.Convert(stat.Average, 6, unit);
                    Statistics = FormatStats(stat.Count, stat.Min, stat.Max, stat.Average, stat.StdDev, unit);
                    Status = $"Burst of {stat.Count} via {SelectedProvider}.";
                }
            });
        }

        private void StartMonitor()
        {
            try
            {
                _running.Reset();
                _monitorDmm = OpenDmm(out _monitorSession);
                _monitorUnit = UnitFor(ApplyConfig(_monitorDmm));

                _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(50, IntervalMs)) };
                _timer.Tick += OnMonitorTick;
                _timer.Start();

                IsMonitoring = true;
                Status = $"Monitoring {Resource} via {SelectedProvider}…";
            }
            catch (Exception ex)
            {
                Status = "Monitor failed: " + ex.Message;
                CleanupMonitor();
            }
        }

        private void OnMonitorTick(object sender, EventArgs e)
        {
            try
            {
                double v = _monitorDmm.ReadValue();
                _running.Add(v);
                LastValue = ToEngineeringFormat.Convert(v, 6, _monitorUnit);
                Statistics = FormatStats(_running.Count, _running.Min, _running.Max, _running.Average, _running.StdDev, _monitorUnit);
            }
            catch (Exception ex)
            {
                Status = "Read error: " + ex.Message;
                StopMonitor();
            }
        }

        private void StopMonitor()
        {
            CleanupMonitor();
            IsMonitoring = false;
            Status = $"Stopped after {_running.Count} reading(s).";
        }

        private void CleanupMonitor()
        {
            if (_timer != null) { _timer.Stop(); _timer.Tick -= OnMonitorTick; _timer = null; }
            _monitorSession?.Dispose();
            _monitorSession = null;
            _monitorDmm = null;
        }

        private static string FormatStats(int n, double min, double max, double avg, double sd, string unit) =>
            string.Format(CultureInfo.InvariantCulture, "n={0}   min={1}   max={2}   avg={3}   sd={4}",
                n,
                ToEngineeringFormat.Convert(min, 6, unit),
                ToEngineeringFormat.Convert(max, 6, unit),
                ToEngineeringFormat.Convert(avg, 6, unit),
                ToEngineeringFormat.Convert(sd, 6, unit));

        private void Guard(Action action)
        {
            try { action(); }
            catch (Exception ex) { Status = "Error: " + ex.Message; }
        }
    }
}
