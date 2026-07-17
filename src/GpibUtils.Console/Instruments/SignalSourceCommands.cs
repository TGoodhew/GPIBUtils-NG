using System;
using System.ComponentModel;
using GpibUtils.Instruments.SignalSources;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>
    /// Generic CLI plumbing shared by the ISignalSource RF-generator drivers. Each device gets a tiny
    /// settings subclass that knows how to open its driver; the <c>idn</c>/<c>apply</c> commands are generic
    /// over that settings type, so a new source needs only a settings class + two branch registrations.
    /// </summary>
    public abstract class SignalSourceSettings : InstrumentSettings
    {
        [CommandOption("-f|--frequency <MHZ>")]
        [Description("CW frequency in MHz.")]
        public double? FrequencyMHz { get; set; }

        [CommandOption("-l|--level <DBM>")]
        [Description("Output power in dBm.")]
        public double? LevelDbm { get; set; }

        [CommandOption("--rf <STATE>")]
        [Description("RF output on | off.")]
        public string Rf { get; set; }

        internal abstract ISignalSource OpenSource(out Visa.IInstrumentSession session);
        internal abstract System.Collections.Generic.IReadOnlyList<string> HistoryOf(ISignalSource source);
    }

    internal static class SignalSourceRunner
    {
        public static int Run(SignalSourceSettings settings, Func<ISignalSource, string> action) => Runner.Guard(() =>
        {
            var source = settings.OpenSource(out var session);
            using (session)
            {
                string result = action(source);
                foreach (var sent in settings.HistoryOf(source))
                    AnsiConsole.MarkupLineInterpolated($"[grey]sent[/]: [green]{sent}[/]");
                if (!string.IsNullOrEmpty(result))
                    AnsiConsole.MarkupLineInterpolated($"[green]{result}[/]");
            }
            return 0;
        });
    }

    /// <summary>Set frequency/level (and RF on/off) on the source.</summary>
    public sealed class SignalSourceApplyCommand<TSettings> : Command<TSettings>
        where TSettings : SignalSourceSettings
    {
        public override int Execute(CommandContext context, TSettings settings) =>
            SignalSourceRunner.Run(settings, s =>
            {
                if (settings.FrequencyMHz.HasValue) s.SetFrequencyMHz(settings.FrequencyMHz.Value);
                if (settings.LevelDbm.HasValue) s.SetPowerDbm(settings.LevelDbm.Value);
                if (string.Equals(settings.Rf, "on", StringComparison.OrdinalIgnoreCase)) s.RfOn();
                else if (string.Equals(settings.Rf, "off", StringComparison.OrdinalIgnoreCase)) s.RfOff();
                return "applied";
            });
    }

    // ---- per-device settings (open driver + expose its History) --------------

    public sealed class E4436BSourceSettings : SignalSourceSettings
    {
        internal override ISignalSource OpenSource(out Visa.IInstrumentSession session)
        { session = OpenSession("e4436b", AgilentE4436B.DefaultResource); return new AgilentE4436B(session); }
        internal override System.Collections.Generic.IReadOnlyList<string> HistoryOf(ISignalSource s) => ((AgilentE4436B)s).History;
    }

    public sealed class Hp83620ASourceSettings : SignalSourceSettings
    {
        internal override ISignalSource OpenSource(out Visa.IInstrumentSession session)
        { session = OpenSession("hp83620a", Hp83620A.DefaultResource); return new Hp83620A(session); }
        internal override System.Collections.Generic.IReadOnlyList<string> HistoryOf(ISignalSource s) => ((Hp83620A)s).History;
    }

    public sealed class Hp83712BSourceSettings : SignalSourceSettings
    {
        internal override ISignalSource OpenSource(out Visa.IInstrumentSession session)
        { session = OpenSession("hp83712b", Hp83712B.DefaultResource); return new Hp83712B(session); }
        internal override System.Collections.Generic.IReadOnlyList<string> HistoryOf(ISignalSource s) => ((Hp83712B)s).History;
    }

    public sealed class Hp8656SourceSettings : SignalSourceSettings
    {
        internal override ISignalSource OpenSource(out Visa.IInstrumentSession session)
        { session = OpenSession("hp8656", Hp8656.DefaultResource); return new Hp8656(session); }
        internal override System.Collections.Generic.IReadOnlyList<string> HistoryOf(ISignalSource s) => ((Hp8656)s).History;
    }

    public sealed class Hp8657BSourceSettings : SignalSourceSettings
    {
        internal override ISignalSource OpenSource(out Visa.IInstrumentSession session)
        { session = OpenSession("hp8657b", Hp8657B.DefaultResource); return new Hp8657B(session); }
        internal override System.Collections.Generic.IReadOnlyList<string> HistoryOf(ISignalSource s) => ((Hp8657B)s).History;
    }

    public sealed class Hp8664ASourceSettings : SignalSourceSettings
    {
        internal override ISignalSource OpenSource(out Visa.IInstrumentSession session)
        { session = OpenSession("hp8664a", Hp8664A.DefaultResource); return new Hp8664A(session); }
        internal override System.Collections.Generic.IReadOnlyList<string> HistoryOf(ISignalSource s) => ((Hp8664A)s).History;
    }

    public sealed class RsSmeSourceSettings : SignalSourceSettings
    {
        internal override ISignalSource OpenSource(out Visa.IInstrumentSession session)
        { session = OpenSession("rs-sme", RohdeSchwarzSme.DefaultResource); return new RohdeSchwarzSme(session); }
        internal override System.Collections.Generic.IReadOnlyList<string> HistoryOf(ISignalSource s) => ((RohdeSchwarzSme)s).History;
    }

    public sealed class RsSmtSourceSettings : SignalSourceSettings
    {
        internal override ISignalSource OpenSource(out Visa.IInstrumentSession session)
        { session = OpenSession("rs-smt", RohdeSchwarzSmt.DefaultResource); return new RohdeSchwarzSmt(session); }
        internal override System.Collections.Generic.IReadOnlyList<string> HistoryOf(ISignalSource s) => ((RohdeSchwarzSmt)s).History;
    }
}
