using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GpibUtils.Common;
using GpibUtils.Console.Instruments;
using GpibUtils.Instruments.Meters;
using Spectre.Console;

namespace GpibUtils.Console.Tui
{
    /// <summary>
    /// The interactive DMM control panel for the HP/Agilent/Keysight 34401A (issue #172, DMM increment):
    /// configure a function/range/NPLC, take a single reading or a burst with statistics, and run a live
    /// dashboard that streams the value + running min/max/avg/sd + a sparkline. It exposes the same DMM
    /// capabilities as the CLI (<c>hp34401a measure/stats/monitor</c>) and the WPF DMM tab — UI parity, one
    /// interactive presentation. Reads happen over the active provider from the shared <see cref="TuiSession"/>.
    /// </summary>
    internal sealed class DmmPanel
    {
        // Function keys in menu order (parsed to MeasurementFunction via the shared DmmFunctionParser).
        private static readonly string[] FunctionKeys =
            { "dcv", "acv", "dci", "aci", "res", "fres", "freq", "per", "cont", "diode" };

        private readonly TuiSession _session;
        private string _resource;
        private string _functionKey = "dcv";
        private string _range;      // null = autorange
        private double? _nplc;      // null = instrument default
        private int _intervalMs = 300;

        public DmmPanel(TuiSession session, string defaultResource)
        {
            _session = session;
            _resource = defaultResource;
        }

        public void Run()
        {
            _resource = AnsiConsole.Prompt(new TextPrompt<string>("VISA [teal]resource[/]:").DefaultValue(_resource));

            while (true)
            {
                AnsiConsole.Clear();
                AnsiConsole.Write(new Rule("[teal]DMM — HP 34401A[/]").LeftJustified().RuleStyle(Style.Parse("grey")));
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLineInterpolated($"[grey]Resource[/] {_resource}   [grey]Provider[/] {_session.ProviderName}");
                AnsiConsole.MarkupLineInterpolated($"[grey]Function[/] {_functionKey}   [grey]Range[/] {_range ?? "auto"}   [grey]NPLC[/] {(_nplc.HasValue ? _nplc.Value.ToString(CultureInfo.InvariantCulture) : "default")}   [grey]Interval[/] {_intervalMs} ms");
                AnsiConsole.WriteLine();

                const string readOnce = "Read once";
                const string burst = "Burst (statistics)";
                const string monitor = "Live monitor (dashboard)";
                const string configure = "Configure (function / range / NPLC / interval)";
                const string changeRes = "Change resource";
                const string back = "← Back";

                var action = AnsiConsole.Prompt(new SelectionPrompt<string>()
                    .Title("DMM action")
                    .HighlightStyle(new Style(foreground: Color.Teal))
                    .AddChoices(readOnce, burst, monitor, configure, changeRes, back));

                if (action == back) return;
                if (action == readOnce) ReadOnce();
                else if (action == burst) Burst();
                else if (action == monitor) LiveMonitor();
                else if (action == configure) Configure();
                else if (action == changeRes)
                    _resource = AnsiConsole.Prompt(new TextPrompt<string>("VISA [teal]resource[/]:").DefaultValue(_resource));
            }
        }

        private void Configure()
        {
            _functionKey = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("Measurement [teal]function[/]")
                .HighlightStyle(new Style(foreground: Color.Teal))
                .AddChoices(FunctionKeys));

            var range = AnsiConsole.Prompt(new TextPrompt<string>("Range [grey](blank = autorange)[/]:")
                .AllowEmpty()
                .DefaultValue(_range ?? string.Empty));
            _range = string.IsNullOrWhiteSpace(range) ? null : range.Trim();

            var nplc = AnsiConsole.Prompt(new TextPrompt<string>("NPLC [grey](blank = default)[/]:")
                .AllowEmpty()
                .DefaultValue(_nplc.HasValue ? _nplc.Value.ToString(CultureInfo.InvariantCulture) : string.Empty));
            _nplc = double.TryParse(nplc, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : (double?)null;

            var interval = AnsiConsole.Prompt(new TextPrompt<int>("Monitor interval (ms):").DefaultValue(_intervalMs));
            _intervalMs = Math.Max(0, interval);
        }

        private void ReadOnce()
        {
            RunGuarded(dmm =>
            {
                var fn = ApplyConfig(dmm);
                double v = dmm.ReadValue();
                AnsiConsole.Write(new Panel(new Markup($"[teal]{Format(fn, v)}[/]"))
                    .Header($" {_functionKey} ").Border(BoxBorder.Rounded).BorderColor(Color.Grey));
            });
            Pause();
        }

        private void Burst()
        {
            int count = AnsiConsole.Prompt(new TextPrompt<int>("Samples:").DefaultValue(100));
            RunGuarded(dmm =>
            {
                var fn = ApplyConfig(dmm);
                var values = dmm.ReadValues(count);
                var s = DmmStatistics.Of(values);
                var unit = UnitFor(fn);

                var table = new Table().Border(TableBorder.Rounded);
                table.AddColumns("Stat", "Value");
                table.AddRow("n", s.Count.ToString(CultureInfo.InvariantCulture));
                table.AddRow("min", ToEngineeringFormat.Convert(s.Min, 6, unit));
                table.AddRow("max", ToEngineeringFormat.Convert(s.Max, 6, unit));
                table.AddRow("avg", ToEngineeringFormat.Convert(s.Average, 6, unit));
                table.AddRow("sd", ToEngineeringFormat.Convert(s.StdDev, 6, unit));
                AnsiConsole.Write(table);
            });
            Pause();
        }

        private void LiveMonitor()
        {
            try
            {
                using (var session = _session.Open(_resource))
                {
                    var dmm = new Hp34401A(session);
                    var fn = ApplyConfig(dmm);
                    var unit = UnitFor(fn);

                    var running = new RunningStatistics();
                    var recent = new List<double>();

                    var layout = new Layout("root").SplitRows(
                        new Layout("reading"),
                        new Layout("stats"),
                        new Layout("spark"));
                    layout["reading"].Update(new Panel("[grey]starting…[/]").Expand());
                    layout["stats"].Update(new Panel("").Expand());
                    layout["spark"].Update(new Panel("").Expand());

                    AnsiConsole.Live(layout).Start(ctx =>
                    {
                        while (true)
                        {
                            if (System.Console.KeyAvailable) { System.Console.ReadKey(true); break; }

                            try
                            {
                                double v = dmm.ReadValue();
                                running.Add(v);
                                recent.Add(v);
                                if (recent.Count > 240) recent.RemoveAt(0);

                                layout["reading"].Update(new Panel(new Markup($"[teal]{Format(fn, v)}[/]"))
                                    .Header($" {_functionKey}  [grey](press any key to stop)[/] ")
                                    .Border(BoxBorder.Rounded).BorderColor(Color.Grey).Expand());

                                var stats = new Table().Border(TableBorder.None).HideHeaders();
                                stats.AddColumns("k", "v");
                                stats.AddRow("[grey]n[/]", running.Count.ToString(CultureInfo.InvariantCulture));
                                stats.AddRow("[grey]min[/]", ToEngineeringFormat.Convert(running.Min, 6, unit));
                                stats.AddRow("[grey]max[/]", ToEngineeringFormat.Convert(running.Max, 6, unit));
                                stats.AddRow("[grey]avg[/]", ToEngineeringFormat.Convert(running.Average, 6, unit));
                                stats.AddRow("[grey]sd[/]", ToEngineeringFormat.Convert(running.StdDev, 6, unit));
                                layout["stats"].Update(new Panel(stats).Header(" statistics ").Border(BoxBorder.Rounded).BorderColor(Color.Grey).Expand());

                                layout["spark"].Update(new Panel(new Markup($"[teal]{Markup.Escape(Sparkline.Render(recent, 120))}[/]"))
                                    .Header(" trend ").Border(BoxBorder.Rounded).BorderColor(Color.Grey).Expand());
                            }
                            catch (Exception ex)
                            {
                                layout["reading"].Update(new Panel(new Markup($"[red]{Markup.Escape(ex.Message)}[/]"))
                                    .Header($" {_functionKey}  [grey](press any key to stop)[/] ")
                                    .Border(BoxBorder.Rounded).BorderColor(Color.Red).Expand());
                            }

                            ctx.Refresh();
                            if (_intervalMs > 0) System.Threading.Thread.Sleep(_intervalMs);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]Error:[/] {ex.Message}");
                Pause();
            }
        }

        // ---- helpers --------------------------------------------------------------------------------

        private MeasurementFunction ApplyConfig(Hp34401A dmm)
        {
            var fn = DmmFunctionParser.Parse(_functionKey);
            dmm.Configure(fn, _range);
            if (_nplc.HasValue) dmm.SetNplc(fn, _nplc.Value);
            return fn;
        }

        private void RunGuarded(Action<Hp34401A> action)
        {
            try
            {
                using (var session = _session.Open(_resource))
                    action(new Hp34401A(session));
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]Error:[/] {ex.Message}");
            }
        }

        private static string Format(MeasurementFunction fn, double value) =>
            ToEngineeringFormat.Convert(value, 6, UnitFor(fn));

        private static string UnitFor(MeasurementFunction fn)
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

        private static void Pause()
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]Press any key to continue…[/]");
            System.Console.ReadKey(true);
        }
    }
}
