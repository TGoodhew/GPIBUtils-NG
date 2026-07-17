using System;
using System.Collections.Generic;
using System.ComponentModel;
using GpibUtils.Instruments.Scopes;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>
    /// Generic CLI plumbing shared by the IOscilloscope drivers. Each scope gets a tiny settings subclass that
    /// opens its driver; the single <c>ctl</c> command (generic over that settings type) does identify /
    /// acquisition / channel-display / Vpp in one invocation, so a new scope needs only a settings class + one
    /// branch registration.
    /// </summary>
    public abstract class ScopeSettings : InstrumentSettings
    {
        [CommandOption("--idn")]
        [Description("Print the instrument identity.")]
        public bool ShowIdn { get; set; }

        [CommandOption("--acq <STATE>")]
        [Description("Acquisition: run | stop | single | auto (autoscale).")]
        public string Acq { get; set; }

        [CommandOption("-c|--channel <N>")]
        [Description("Channel number (for --display / --vpp).")]
        public int? Channel { get; set; }

        [CommandOption("--display <STATE>")]
        [Description("Turn the --channel trace on | off.")]
        public string Display { get; set; }

        [CommandOption("--vpp")]
        [Description("Measure peak-to-peak volts on --channel.")]
        public bool Vpp { get; set; }

        internal abstract IOscilloscope OpenScope(out Visa.IInstrumentSession session);
        internal abstract IReadOnlyList<string> HistoryOf(IOscilloscope scope);
    }

    public sealed class ScopeCtlCommand<TSettings> : Command<TSettings>
        where TSettings : ScopeSettings
    {
        public override int Execute(CommandContext context, TSettings s) => Runner.Guard(() =>
        {
            var scope = s.OpenScope(out var session);
            using (session)
            {
                string result = null;
                if (s.ShowIdn) result = scope.Identify();
                if (!string.IsNullOrWhiteSpace(s.Acq))
                {
                    switch (s.Acq.Trim().ToLowerInvariant())
                    {
                        case "run": scope.Run(); break;
                        case "stop": scope.Stop(); break;
                        case "single": scope.Single(); break;
                        case "auto": case "autoscale": scope.AutoScale(); break;
                        default: throw new ArgumentException($"Unknown --acq state '{s.Acq}'.");
                    }
                }
                if (s.Channel.HasValue && !string.IsNullOrWhiteSpace(s.Display))
                    scope.SetChannelDisplay(s.Channel.Value, string.Equals(s.Display, "on", StringComparison.OrdinalIgnoreCase));
                if (s.Vpp && s.Channel.HasValue)
                    result = $"Vpp(CH{s.Channel}) = {scope.MeasureVpp(s.Channel.Value)} V";

                foreach (var sent in s.HistoryOf(scope))
                    AnsiConsole.MarkupLineInterpolated($"[grey]sent[/]: [green]{sent}[/]");
                if (!string.IsNullOrEmpty(result))
                    AnsiConsole.MarkupLineInterpolated($"[green]{result}[/]");
            }
            return 0;
        });
    }

    // ---- per-device settings -------------------------------------------------

    public sealed class Dpo3000ScopeSettings : ScopeSettings
    {
        internal override IOscilloscope OpenScope(out Visa.IInstrumentSession session)
        { session = OpenSession("dpo3000", TektronixDpo3000.DefaultResource); return new TektronixDpo3000(session); }
        internal override IReadOnlyList<string> HistoryOf(IOscilloscope s) => ((TektronixDpo3000)s).History;
    }

    public sealed class Dpo4000ScopeSettings : ScopeSettings
    {
        internal override IOscilloscope OpenScope(out Visa.IInstrumentSession session)
        { session = OpenSession("dpo4000", TektronixDpo4000.DefaultResource); return new TektronixDpo4000(session); }
        internal override IReadOnlyList<string> HistoryOf(IOscilloscope s) => ((TektronixDpo4000)s).History;
    }

    public sealed class Tds784ScopeSettings : ScopeSettings
    {
        internal override IOscilloscope OpenScope(out Visa.IInstrumentSession session)
        { session = OpenSession("tds784", TektronixTds784.DefaultResource); return new TektronixTds784(session); }
        internal override IReadOnlyList<string> HistoryOf(IOscilloscope s) => ((TektronixTds784)s).History;
    }

    public sealed class Hp54622ScopeSettings : ScopeSettings
    {
        internal override IOscilloscope OpenScope(out Visa.IInstrumentSession session)
        { session = OpenSession("hp54622", Hp54622A.DefaultResource); return new Hp54622A(session); }
        internal override IReadOnlyList<string> HistoryOf(IOscilloscope s) => ((Hp54622A)s).History;
    }

    public sealed class Hp54845ScopeSettings : ScopeSettings
    {
        internal override IOscilloscope OpenScope(out Visa.IInstrumentSession session)
        { session = OpenSession("hp54845a", Hp54845A.DefaultResource); return new Hp54845A(session); }
        internal override IReadOnlyList<string> HistoryOf(IOscilloscope s) => ((Hp54845A)s).History;
    }

    public sealed class Lc574aScopeSettings : ScopeSettings
    {
        internal override IOscilloscope OpenScope(out Visa.IInstrumentSession session)
        { session = OpenSession("lc574a", LeCroyLC574A.DefaultResource); return new LeCroyLC574A(session); }
        internal override IReadOnlyList<string> HistoryOf(IOscilloscope s) => ((LeCroyLC574A)s).History;
    }

    public sealed class WaveRunner6000ScopeSettings : ScopeSettings
    {
        internal override IOscilloscope OpenScope(out Visa.IInstrumentSession session)
        { session = OpenSession("waverunner6000", LeCroyWaveRunner6000.DefaultResource); return new LeCroyWaveRunner6000(session); }
        internal override IReadOnlyList<string> HistoryOf(IOscilloscope s) => ((LeCroyWaveRunner6000)s).History;
    }
}
