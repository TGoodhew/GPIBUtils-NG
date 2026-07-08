using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using GpibUtils.Common;
using GpibUtils.Visa;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console
{
    /// <summary>Shared option: which provider to use (defaults to the registry default, e.g. NI-VISA).</summary>
    public class ProviderSettings : CommandSettings
    {
        [CommandOption("-p|--provider <NAME>")]
        [Description("Provider name (e.g. NI-VISA, Simulated). Defaults to the registry default.")]
        public string Provider { get; set; }

        internal IGpibProvider Resolve() =>
            string.IsNullOrWhiteSpace(Provider) ? GpibProviders.Default : GpibProviders.Get(Provider);
    }

    /// <summary>Lists registered providers and what each can do.</summary>
    public sealed class ProvidersCommand : Command<ProvidersCommand.Settings>
    {
        public sealed class Settings : CommandSettings { }

        public override int Execute(CommandContext context, Settings settings)
        {
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumns("Provider", "Available", "Default", "Discover", "SerialPoll", "SRQ", "Clear", "Local", "Native");

            foreach (var p in GpibProviders.All.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                var c = p.Capabilities;
                table.AddRow(
                    p.Name,
                    p.IsAvailable ? "[green]yes[/]" : "[grey]no[/]",
                    string.Equals(p.Name, GpibProviders.DefaultProviderName, StringComparison.OrdinalIgnoreCase) ? "[green]*[/]" : "",
                    Mark(c.Discovery), Mark(c.SerialPoll), Mark(c.ServiceRequest),
                    Mark(c.DeviceClear), Mark(c.ReturnToLocal), Mark(c.NativeAddressing));
            }

            AnsiConsole.Write(table);

            var unavailable = GpibProviders.All.Where(p => !p.IsAvailable && !string.IsNullOrWhiteSpace(p.UnavailableReason)).ToList();
            if (unavailable.Any())
            {
                AnsiConsole.WriteLine();
                foreach (var p in unavailable)
                    AnsiConsole.MarkupLineInterpolated($"[grey]{p.Name}: {p.UnavailableReason}[/]");
            }
            return 0;
        }

        private static string Mark(bool on) => on ? "[green]+[/]" : "[grey]-[/]";
    }

    /// <summary>Discovers instruments visible to a provider.</summary>
    public sealed class DiscoverCommand : Command<DiscoverCommand.Settings>
    {
        public sealed class Settings : ProviderSettings
        {
            [CommandOption("-f|--filter <FILTER>")]
            [Description("VISA resource filter (default ?*::INSTR).")]
            public string Filter { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings)
        {
            return Runner.Guard(() =>
            {
                var provider = settings.Resolve();
                var found = provider.Discover(string.IsNullOrWhiteSpace(settings.Filter) ? "?*::INSTR" : settings.Filter);
                if (found.Count == 0)
                {
                    AnsiConsole.MarkupLineInterpolated($"[yellow]No instruments found via {provider.Name}.[/]");
                    return 0;
                }
                AnsiConsole.MarkupLineInterpolated($"[green]{found.Count}[/] instrument(s) via {provider.Name}:");
                foreach (var r in found) AnsiConsole.WriteLine("  " + r);
                return 0;
            });
        }
    }

    /// <summary>Opens a resource, sends one command, prints the reply.</summary>
    public sealed class QueryCommand : Command<QueryCommand.Settings>
    {
        public sealed class Settings : ProviderSettings
        {
            [CommandArgument(0, "<resource>")]
            [Description("VISA resource string, e.g. GPIB0::14::INSTR.")]
            public string Resource { get; set; }

            [CommandArgument(1, "<command>")]
            [Description("Command to send, e.g. \"*IDN?\".")]
            public string Scpi { get; set; }

            [CommandOption("-t|--timeout <MS>")]
            [Description("I/O timeout in milliseconds (default 5000).")]
            public int TimeoutMs { get; set; } = 5000;

            [CommandOption("-e|--engineering <UNIT>")]
            [Description("If the reply is numeric, also show it in engineering notation with this unit.")]
            public string EngineeringUnit { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings)
        {
            return Runner.Guard(() =>
            {
                var provider = settings.Resolve();
                using (var session = provider.Open(settings.Resource, new SessionSettings { TimeoutMilliseconds = settings.TimeoutMs }))
                {
                    var reply = session.Query(settings.Scpi);
                    AnsiConsole.MarkupLineInterpolated($"[green]{reply}[/]");

                    if (!string.IsNullOrWhiteSpace(settings.EngineeringUnit) &&
                        double.TryParse(reply, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                    {
                        AnsiConsole.MarkupLineInterpolated($"[blue]{ToEngineeringFormat.Convert(value, 4, settings.EngineeringUnit)}[/]");
                    }
                }
                return 0;
            });
        }
    }

    /// <summary>Shortcut for <c>query &lt;resource&gt; *IDN?</c>.</summary>
    public sealed class IdnCommand : Command<IdnCommand.Settings>
    {
        public sealed class Settings : ProviderSettings
        {
            [CommandArgument(0, "<resource>")]
            [Description("VISA resource string, e.g. GPIB0::14::INSTR.")]
            public string Resource { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings)
        {
            return Runner.Guard(() =>
            {
                var provider = settings.Resolve();
                using (var session = provider.Open(settings.Resource))
                    AnsiConsole.MarkupLineInterpolated($"[green]{session.Query("*IDN?")}[/]");
                return 0;
            });
        }
    }

    /// <summary>Runs an action, turning provider/GPIB failures into a clean message + non-zero exit.</summary>
    internal static class Runner
    {
        public static int Guard(Func<int> action)
        {
            try
            {
                return action();
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]Error:[/] {ex.Message}");
                return 1;
            }
        }
    }
}
