using System;
using System.ComponentModel;
using GpibUtils.Instruments.Switches;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>Shared options for every <c>hp3499a</c> subcommand (the standard instrument options).</summary>
    public class Hp3499ASettings : InstrumentSettings
    {
        /// <summary>Opens a session (default GPIB0::9::INSTR) and builds the driver.</summary>
        internal Hp3499A OpenDriver(out Visa.IInstrumentSession session)
        {
            session = OpenSession("hp3499a", Hp3499A.DefaultResource);
            return new Hp3499A(session);
        }
    }

    /// <summary>Shared execution shell: open, run, echo the commands sent, and (optionally) print a result.</summary>
    internal static class Hp3499ARunner
    {
        public static int Run(Hp3499ASettings settings, Func<Hp3499A, string> action) => Runner.Guard(() =>
        {
            var driver = settings.OpenDriver(out var session);
            using (session)
            {
                string result = action(driver);
                foreach (var sent in driver.History)
                    AnsiConsole.MarkupLineInterpolated($"[grey]sent[/]: [green]{sent}[/]");
                if (!string.IsNullOrEmpty(result))
                    AnsiConsole.MarkupLineInterpolated($"[green]{result}[/]");
            }
            return 0;
        });
    }

    /// <summary>Options for the commands that act on a single relay channel.</summary>
    public class Hp3499AChannelSettings : Hp3499ASettings
    {
        [CommandArgument(0, "<channel>")]
        [Description("Channel address snn (slot + two-digit channel), e.g. 100 = slot 1, channel 00.")]
        public int Channel { get; set; }
    }

    /// <summary>Query the instrument identity (*IDN?).</summary>
    public sealed class Hp3499AIdnCommand : Command<Hp3499AIdnCommand.Settings>
    {
        public sealed class Settings : Hp3499ASettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp3499ARunner.Run(settings, d => d.Identify());
    }

    /// <summary>Device clear + reset + status preset (clean known state).</summary>
    public sealed class Hp3499AInitCommand : Command<Hp3499AInitCommand.Settings>
    {
        public sealed class Settings : Hp3499ASettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp3499ARunner.Run(settings, d => { d.Initialize(); return null; });
    }

    /// <summary>List the plug-in cards installed in each slot (SYSTem:CTYPE?).</summary>
    public sealed class Hp3499ACardsCommand : Command<Hp3499ACardsCommand.Settings>
    {
        public sealed class Settings : Hp3499ASettings
        {
            [CommandOption("--slots <N>")]
            [Description("Number of slots to enumerate, from slot 0 (default 6).")]
            public int Slots { get; set; } = Hp3499A.DefaultSlotCount;
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp3499ARunner.Run(settings, d =>
            {
                var cards = d.ListCards(settings.Slots);
                var table = new Table().AddColumn("Slot").AddColumn("Card type");
                foreach (var c in cards)
                    table.AddRow(c.Slot.ToString(), Markup.Escape(c.CardType));
                AnsiConsole.Write(table);
                return null;
            });
    }

    /// <summary>Close a relay channel.</summary>
    public sealed class Hp3499ACloseCommand : Command<Hp3499ACloseCommand.Settings>
    {
        public sealed class Settings : Hp3499AChannelSettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp3499ARunner.Run(settings, d => { d.Close(settings.Channel); return $"closed (@{settings.Channel})"; });
    }

    /// <summary>Open a relay channel.</summary>
    public sealed class Hp3499AOpenCommand : Command<Hp3499AOpenCommand.Settings>
    {
        public sealed class Settings : Hp3499AChannelSettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp3499ARunner.Run(settings, d => { d.Open(settings.Channel); return $"opened (@{settings.Channel})"; });
    }

    /// <summary>Query whether a relay channel is closed.</summary>
    public sealed class Hp3499AStateCommand : Command<Hp3499AStateCommand.Settings>
    {
        public sealed class Settings : Hp3499AChannelSettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp3499ARunner.Run(settings, d => $"(@{settings.Channel}) is {(d.IsClosed(settings.Channel) ? "closed" : "open")}");
    }
}
