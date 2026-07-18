using System;
using System.Collections.Generic;
using System.Linq;
using GpibUtils.Visa;
using Spectre.Console;

namespace GpibUtils.Console.Tui
{
    /// <summary>
    /// The interactive, menu-driven Spectre.Console TUI front-end (issue #172). Launched by running
    /// <c>gpibutils</c> with no verb on a terminal, or the explicit <c>tui</c> command. It is a third
    /// presentation of the shared driver/provider core alongside the one-shot CLI and the WPF app: this
    /// first increment surfaces only capabilities that already exist in <b>both</b> other front-ends —
    /// Providers, Discover, and a Query console — so it adds no UI-exclusive features (the UI-parity rule).
    ///
    /// <para>Structured as a prompt-driven state machine: a top-level menu loop dispatches to screens, and
    /// each screen uses blocking prompts (Spectre's <c>Live</c>/<c>Status</c>/prompts cannot nest).</para>
    /// </summary>
    public sealed class TuiApp
    {
        private const string BackLabel = "← Back";
        private static readonly CatalogInstrument BackInstrument = new CatalogInstrument("←", "Back", "");

        private readonly TuiSession _session;
        private string _lastResource = "GPIB0::5::INSTR";

        public TuiApp(string initialProvider = null)
        {
            _session = new TuiSession(initialProvider);
        }

        /// <summary>Runs the menu loop until the user exits. Returns a process exit code (0 = normal).</summary>
        public int Run()
        {
            if (IsInputRedirected())
            {
                AnsiConsole.MarkupLine("[red]The interactive UI needs a terminal (stdin is redirected).[/] " +
                                       "Use the one-shot commands instead — run [green]gpibutils --help[/].");
                return 1;
            }

            while (true)
            {
                AnsiConsole.Clear();
                RenderHeader();

                var item = AnsiConsole.Prompt(new SelectionPrompt<TuiMenuItem>()
                    .Title("[grey]Choose a screen[/] [grey](↑/↓, Enter)[/]")
                    .HighlightStyle(new Style(foreground: Color.Teal))
                    .UseConverter(i => $"{i.Label}  [grey]— {i.Description}[/]")
                    .AddChoices(TuiMenu.Items));

                switch (item.Screen)
                {
                    case TuiScreen.Providers: ProvidersScreen(); break;
                    case TuiScreen.Discover: DiscoverScreen(); break;
                    case TuiScreen.Instruments: InstrumentsScreen(); break;
                    case TuiScreen.Query: QueryScreen(null); break;
                    case TuiScreen.Exit:
                        AnsiConsole.MarkupLine("[grey]Bye.[/]");
                        return 0;
                }
            }
        }

        // ---- Header ---------------------------------------------------------------------------------

        private void RenderHeader()
        {
            AnsiConsole.Write(new FigletText("GPIBUtils").LeftJustified().Color(Color.Teal));

            string availability;
            try
            {
                var p = _session.ResolveProvider();
                availability = p.IsAvailable
                    ? "[green]available[/]"
                    : $"[red]unavailable[/] [grey]({Markup.Escape(p.UnavailableReason ?? "not present")})[/]";
            }
            catch (Exception ex)
            {
                availability = $"[red]error[/] [grey]({Markup.Escape(ex.Message)})[/]";
            }

            var body = $"Provider: [teal]{Markup.Escape(_session.ProviderName)}[/]  •  " +
                       $"Timeout: {_session.TimeoutMs} ms  •  {availability}";
            AnsiConsole.Write(new Panel(new Markup(body))
                .Header(" interactive ")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Grey));
            AnsiConsole.WriteLine();
        }

        // ---- Providers ------------------------------------------------------------------------------

        private void ProvidersScreen()
        {
            Heading("Providers");

            var rows = GpibProviders.All
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Select(p => ProviderCapabilityRow.From(p, GpibProviders.DefaultProviderName))
                .ToList();

            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumns("Provider", "Available", "Default", "Discover", "SerialPoll", "SRQ", "Clear", "Local", "Native");
            foreach (var r in rows)
            {
                table.AddRow(
                    Markup.Escape(r.Name),
                    r.IsAvailable ? "[green]yes[/]" : "[grey]no[/]",
                    r.IsDefault ? "[green]*[/]" : "",
                    Mark(r.Discovery), Mark(r.SerialPoll), Mark(r.ServiceRequest),
                    Mark(r.DeviceClear), Mark(r.ReturnToLocal), Mark(r.NativeAddressing));
            }
            AnsiConsole.Write(table);

            foreach (var r in rows.Where(r => !r.IsAvailable && !string.IsNullOrWhiteSpace(r.UnavailableReason)))
                AnsiConsole.MarkupLineInterpolated($"[grey]{r.Name}: {r.UnavailableReason}[/]");
            AnsiConsole.WriteLine();

            var choices = new List<string> { BackLabel };
            choices.AddRange(rows.Select(r => r.Name));
            var pick = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("Set the [teal]active provider[/] for this session")
                .HighlightStyle(new Style(foreground: Color.Teal))
                .AddChoices(choices));

            if (pick != BackLabel)
            {
                _session.ProviderName = pick;
                AnsiConsole.MarkupLineInterpolated($"[green]Active provider set to {pick}.[/]");
                Pause();
            }
        }

        // ---- Discover -------------------------------------------------------------------------------

        private void DiscoverScreen()
        {
            Heading("Discover");

            IReadOnlyList<string> found = null;
            string error = null;
            IGpibProvider provider = null;
            AnsiConsole.Status().Start($"Scanning via {_session.ProviderName}…", ctx =>
            {
                try
                {
                    provider = _session.ResolveProvider();
                    found = provider.Discover();
                }
                catch (Exception ex) { error = ex.Message; }
            });

            if (error != null)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]Error:[/] {error}");
                Pause();
                return;
            }

            if (found.Count == 0)
            {
                AnsiConsole.MarkupLineInterpolated($"[yellow]No instruments found via {provider.Name}.[/]");
                Pause();
                return;
            }

            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("#");
            table.AddColumn("Resource");
            for (int i = 0; i < found.Count; i++)
                table.AddRow((i + 1).ToString(), Markup.Escape(found[i]));
            AnsiConsole.Write(table);

            if (DiscoveryAdvisory.IsExtenderPhantom(found.Count))
            {
                AnsiConsole.MarkupLine(
                    "[yellow]![/] Nearly every GPIB address reported present — that signature means an " +
                    "HP-IB bus extender (HP 37204A or similar) is in the path. Extenders ACK the address " +
                    "scan for their whole remote segment, so this list is [yellow]phantom[/]. Ignore it and " +
                    "drive instruments by explicit address.");
            }
            else
            {
                AnsiConsole.MarkupLine(
                    "[grey]Note: bus extenders (e.g. HP 37204A) can make addresses appear present; " +
                    "prefer explicit addresses over discovery.[/]");
            }
            Pause();
        }

        // ---- Instruments ----------------------------------------------------------------------------

        private void InstrumentsScreen()
        {
            while (true)
            {
                Heading("Instruments");
                AnsiConsole.MarkupLine("[grey]Browse the catalog, then open a query console pre-filled with the instrument's default resource.[/]");
                AnsiConsole.WriteLine();

                var groups = InstrumentCatalog.Groups();
                var familyChoices = new List<string> { BackLabel };
                familyChoices.AddRange(groups.Select(g => $"{g.Name}  [grey]({g.Instruments.Count})[/]"));

                var familyPick = AnsiConsole.Prompt(new SelectionPrompt<string>()
                    .Title("Pick an instrument [teal]family[/]")
                    .PageSize(20)
                    .HighlightStyle(new Style(foreground: Color.Teal))
                    .MoreChoicesText("[grey](scroll for more)[/]")
                    .AddChoices(familyChoices));
                if (familyPick == BackLabel) return;

                var group = groups[familyChoices.IndexOf(familyPick) - 1];

                var instrPick = AnsiConsole.Prompt(new SelectionPrompt<CatalogInstrument>()
                    .Title($"[teal]{Markup.Escape(group.Name)}[/] — pick an instrument")
                    .PageSize(20)
                    .HighlightStyle(new Style(foreground: Color.Teal))
                    .MoreChoicesText("[grey](scroll for more)[/]")
                    .UseConverter(ci => ReferenceEquals(ci, BackInstrument)
                        ? BackLabel
                        : $"{ci.Key}  [grey]— {Markup.Escape(ci.Description)}[/]")
                    .AddChoices(new[] { BackInstrument }.Concat(group.Instruments)));
                if (ReferenceEquals(instrPick, BackInstrument)) continue;

                InstrumentDetail(instrPick);
            }
        }

        private void InstrumentDetail(CatalogInstrument instrument)
        {
            while (true)
            {
                Heading($"Instrument — {instrument.Key}");
                var grid = new Grid();
                grid.AddColumn(new GridColumn().NoWrap().PadRight(2));
                grid.AddColumn();
                grid.AddRow("[grey]Key[/]", Markup.Escape(instrument.Key));
                grid.AddRow("[grey]Description[/]", Markup.Escape(instrument.Description));
                grid.AddRow("[grey]Default resource[/]", Markup.Escape(instrument.DefaultResource));
                AnsiConsole.Write(grid);
                AnsiConsole.WriteLine();

                const string openConsole = "Open query console (this instrument)";
                const string idnNow = "Send *IDN? now";
                var action = AnsiConsole.Prompt(new SelectionPrompt<string>()
                    .Title("Action")
                    .HighlightStyle(new Style(foreground: Color.Teal))
                    .AddChoices(openConsole, idnNow, BackLabel));

                if (action == BackLabel) return;
                if (action == openConsole) { QueryScreen(instrument.DefaultResource); return; }
                if (action == idnNow) SendIdn(instrument.DefaultResource);
            }
        }

        private void SendIdn(string resource)
        {
            try
            {
                string reply = null;
                AnsiConsole.Status().Start($"Querying *IDN? at {resource}…", _ =>
                {
                    using (var s = _session.Open(resource))
                        reply = s.Query("*IDN?");
                });
                AnsiConsole.MarkupLineInterpolated($"[green]{reply}[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]Error:[/] {ex.Message}");
            }
            Pause();
        }

        // ---- Query console --------------------------------------------------------------------------

        private void QueryScreen(string prefillResource)
        {
            var resource = string.IsNullOrWhiteSpace(prefillResource) ? _lastResource : prefillResource;
            resource = AnsiConsole.Prompt(new TextPrompt<string>("VISA [teal]resource[/]:").DefaultValue(resource));
            _lastResource = resource;

            var transcript = new List<TranscriptLine>();
            while (true)
            {
                Heading($"Query console — {resource}");
                AnsiConsole.MarkupLine($"[grey]Provider [teal]{Markup.Escape(_session.ProviderName)}[/]. " +
                                       "A command ending in '?' is queried; otherwise it is written. " +
                                       "Blank = back, '!' = change resource.[/]");
                AnsiConsole.WriteLine();
                RenderTranscript(transcript);

                var cmd = AnsiConsole.Prompt(new TextPrompt<string>("scpi>").AllowEmpty());
                if (string.IsNullOrEmpty(cmd)) return;

                if (cmd == "!")
                {
                    resource = AnsiConsole.Prompt(new TextPrompt<string>("VISA [teal]resource[/]:").DefaultValue(resource));
                    _lastResource = resource;
                    continue;
                }

                RunOne(resource, cmd, transcript);
            }
        }

        private void RunOne(string resource, string command, List<TranscriptLine> transcript)
        {
            try
            {
                using (var s = _session.Open(resource))
                {
                    if (command.Contains("?"))
                    {
                        var reply = s.Query(command);
                        transcript.Add(new TranscriptLine(command, reply, false));
                    }
                    else
                    {
                        s.Write(command);
                        transcript.Add(new TranscriptLine(command, "(sent)", false));
                    }
                }
            }
            catch (Exception ex)
            {
                transcript.Add(new TranscriptLine(command, ex.Message, true));
            }
        }

        private static void RenderTranscript(List<TranscriptLine> transcript)
        {
            if (transcript.Count == 0)
            {
                AnsiConsole.MarkupLine("[grey](no commands yet)[/]");
                AnsiConsole.WriteLine();
                return;
            }

            const int show = 12;
            foreach (var line in transcript.Skip(Math.Max(0, transcript.Count - show)))
            {
                AnsiConsole.MarkupLineInterpolated($"[grey]›[/] [white]{line.Command}[/]");
                if (line.IsError)
                    AnsiConsole.MarkupLineInterpolated($"  [red]{line.Reply}[/]");
                else
                    AnsiConsole.MarkupLineInterpolated($"  [green]{line.Reply}[/]");
            }
            AnsiConsole.WriteLine();
        }

        private sealed class TranscriptLine
        {
            public string Command { get; }
            public string Reply { get; }
            public bool IsError { get; }
            public TranscriptLine(string command, string reply, bool isError)
            {
                Command = command;
                Reply = reply;
                IsError = isError;
            }
        }

        // ---- Chrome helpers -------------------------------------------------------------------------

        private static void Heading(string title)
        {
            AnsiConsole.Clear();
            AnsiConsole.Write(new Rule($"[teal]{Markup.Escape(title)}[/]").LeftJustified().RuleStyle(Style.Parse("grey")));
            AnsiConsole.WriteLine();
        }

        private static void Pause()
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]Press any key to continue…[/]");
            System.Console.ReadKey(true);
        }

        private static string Mark(bool on) => on ? "[green]+[/]" : "[grey]-[/]";

        private static bool IsInputRedirected()
        {
            try { return System.Console.IsInputRedirected; }
            catch { return false; }
        }
    }
}
