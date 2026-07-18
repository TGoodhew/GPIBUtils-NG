using System;
using System.Collections.Generic;
using System.ComponentModel;
using GpibUtils.Instruments.NetworkAnalyzers;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>Generic CLI plumbing shared by the INetworkAnalyzer drivers.</summary>
    public abstract class NetworkAnalyzerSettings : InstrumentSettings
    {
        [CommandOption("--start <HZ>")] [Description("Sweep start frequency in Hz.")] public double? StartHz { get; set; }
        [CommandOption("--stop <HZ>")] [Description("Sweep stop frequency in Hz.")] public double? StopHz { get; set; }
        [CommandOption("--power <DBM>")] [Description("Source power in dBm.")] public double? PowerDbm { get; set; }
        [CommandOption("-s|--sparam <PARAM>")] [Description("Parameter: s11 | s21 | s12 | s22.")] public string Param { get; set; }
        [CommandOption("--peak")] [Description("After the sweep, report the peak marker.")] public bool Peak { get; set; }
        internal abstract INetworkAnalyzer Open(out Visa.IInstrumentSession session);
        internal abstract IReadOnlyList<string> HistoryOf(INetworkAnalyzer a);
    }

    public sealed class NetworkAnalyzerSweepCommand<TSettings> : Command<TSettings> where TSettings : NetworkAnalyzerSettings
    {
        public override int Execute(CommandContext context, TSettings s) => Runner.Guard(() =>
        {
            var a = s.Open(out var session);
            using (session)
            {
                if (s.StartHz.HasValue) a.SetStartFrequencyHz(s.StartHz.Value);
                if (s.StopHz.HasValue) a.SetStopFrequencyHz(s.StopHz.Value);
                if (s.PowerDbm.HasValue) a.SetSourcePowerDbm(s.PowerDbm.Value);
                if (!string.IsNullOrWhiteSpace(s.Param)) a.SetMeasurement(ParseParam(s.Param));
                a.SingleSweep();
                string result = s.Peak ? $"peak {a.MarkerToPeakY()} at {a.MarkerFrequencyHz() / 1e6:0.###} MHz" : "sweep complete";
                foreach (var sent in s.HistoryOf(a)) AnsiConsole.MarkupLineInterpolated($"[grey]sent[/]: [green]{sent}[/]");
                AnsiConsole.MarkupLineInterpolated($"[green]{result}[/]");
            }
            return 0;
        });

        private static NetworkParameter ParseParam(string p)
        {
            switch ((p ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "s11": return NetworkParameter.S11;
                case "s21": return NetworkParameter.S21;
                case "s12": return NetworkParameter.S12;
                case "s22": return NetworkParameter.S22;
                default: throw new ArgumentException($"Unknown parameter '{p}'.");
            }
        }
    }

    public sealed class Hp8714NaSettings : NetworkAnalyzerSettings
    {
        internal override INetworkAnalyzer Open(out Visa.IInstrumentSession session)
        { session = OpenSession("hp8714", Hp8714.DefaultResource); return new Hp8714(session); }
        internal override IReadOnlyList<string> HistoryOf(INetworkAnalyzer a) => ((Hp8714)a).History;
    }

    public sealed class Hp8720cNaSettings : NetworkAnalyzerSettings
    {
        internal override INetworkAnalyzer Open(out Visa.IInstrumentSession session)
        { session = OpenSession("hp8720c", Hp8720C.DefaultResource); return new Hp8720C(session); }
        internal override IReadOnlyList<string> HistoryOf(INetworkAnalyzer a) => ((Hp8720C)a).History;
    }

    public sealed class Hp8757dNaSettings : NetworkAnalyzerSettings
    {
        internal override INetworkAnalyzer Open(out Visa.IInstrumentSession session)
        { session = OpenSession("hp8757d", Hp8757D.DefaultResource); return new Hp8757D(session); }
        internal override IReadOnlyList<string> HistoryOf(INetworkAnalyzer a) => ((Hp8757D)a).History;
    }
}
