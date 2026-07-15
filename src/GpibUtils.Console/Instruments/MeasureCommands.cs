using System;
using System.ComponentModel;
using GpibUtils.Common;
using GpibUtils.Instruments.Meters;
using GpibUtils.Instruments.SignalSources;
using GpibUtils.Instruments.Switches;
using GpibUtils.Measurement;
using GpibUtils.Visa;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>
    /// Runs the end-to-end attenuation-vs-frequency measurement (issue #34): orchestrates the source
    /// (HP 8340B), LO (HP 8673B), step attenuator (HP 11713A) and measuring receiver (HP 8902A) through a
    /// frequency + attenuation sweep via the ported <see cref="MeasurementEngine"/>, and prints a summary.
    /// </summary>
    public sealed class MeasureSweepCommand : Command<MeasureSweepCommand.Settings>
    {
        public sealed class Settings : ProviderSettings
        {
            [CommandOption("--start <MHZ>")] [Description("Sweep start frequency in MHz (default 1000).")]
            public double StartMHz { get; set; } = 1000;

            [CommandOption("--stop <MHZ>")] [Description("Sweep stop frequency in MHz (default 1000).")]
            public double StopMHz { get; set; } = 1000;

            [CommandOption("--step <MHZ>")] [Description("Frequency step in MHz (default 100).")]
            public double StepMHz { get; set; } = 100;

            [CommandOption("--atten-stop <DB>")] [Description("Maximum attenuation to sweep to, in dB (default 30).")]
            public int AttenStopDb { get; set; } = 30;

            [CommandOption("--atten-step <DB>")] [Description("Attenuation step in dB (default 10).")]
            public int AttenStepDb { get; set; } = 10;

            [CommandOption("--power <DBM>")] [Description("Source power in dBm (default 0).")]
            public double PowerDbm { get; set; } = 0;

            [CommandOption("-t|--timeout <MS>")] [Description("I/O timeout in milliseconds (default 5000).")]
            public int TimeoutMs { get; set; } = 5000;

            [CommandOption("--source <RES>")] [Description("HP 8340B resource (default: configured / manual).")]
            public string Source { get; set; }
            [CommandOption("--lo <RES>")] [Description("HP 8673B LO resource (default: configured / manual).")]
            public string Lo { get; set; }
            [CommandOption("--atten <RES>")] [Description("HP 11713A attenuator resource (default: configured / manual).")]
            public string Atten { get; set; }
            [CommandOption("--receiver <RES>")] [Description("HP 8902A receiver resource (default: configured / manual).")]
            public string Receiver { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) => Runner.Guard(() =>
        {
            var provider = settings.Resolve();
            var store = InstrumentAddressStore.Load();
            IInstrumentSession Open(string explicitAddr, string key, string def) =>
                provider.Open(store.Resolve(explicitAddr, key, def), new SessionSettings { TimeoutMilliseconds = settings.TimeoutMs });

            using (var src = Open(settings.Source, "hp8340b", Hp8340B.DefaultResource))
            using (var lo = Open(settings.Lo, "hp8673b", Hp8673B.DefaultResource))
            using (var att = Open(settings.Atten, "hp11713a", Hp11713A.DefaultResource))
            using (var rcv = Open(settings.Receiver, "hp8902a", Hp8902A.DefaultResource))
            {
                var options = new SweepOptions
                {
                    FreqStartMHz = settings.StartMHz,
                    FreqStopMHz = settings.StopMHz,
                    FreqStepMHz = settings.StepMHz,
                    SourcePowerDbm = settings.PowerDbm,
                    AttenStartDb = 0,
                    AttenStopDb = settings.AttenStopDb,
                    AttenStepDb = settings.AttenStepDb
                };

                var engine = new MeasurementEngine(
                    new Hp8340B(src), new Hp8673B(lo),
                    new Hp11713A(att, AttenuatorConfig.Default()), new Hp8902A(rcv), options);

                var table = new Table().Border(TableBorder.Rounded)
                    .AddColumn("Freq (MHz)").AddColumn("Regime").AddColumn("Points")
                    .AddColumn("Max |err| dB").AddColumn("Deepest dB").AddColumn("Note");

                foreach (var r in engine.RunSweep())
                    table.AddRow(
                        r.FreqMHz.ToString("0.###"),
                        r.Regime.ToString(),
                        r.Points.Count.ToString(),
                        double.IsNaN(r.MaxAbsErrorDb) ? "-" : r.MaxAbsErrorDb.ToString("0.00"),
                        double.IsNaN(r.DeepestMeasuredDb) ? "-" : r.DeepestMeasuredDb.ToString("0.0"),
                        Markup.Escape(r.Warning ?? ""));

                AnsiConsole.Write(table);
                return 0;
            }
        });
    }
}
