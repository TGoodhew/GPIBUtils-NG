using System;
using System.ComponentModel;
using GpibUtils.Mcp;
using GpibUtils.Mcp.Instruments;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>
    /// Runs the GPIBUtils-NG MCP server (issue #41): a JSON-RPC 2.0 server over stdio that lets an MCP
    /// client (e.g. an LLM) discover instruments, exchange SCPI/IEEE-488.2 over the provider model, look up
    /// the instrument-model DB, run #43 SRQ completions, and capture screens via #42. stdout carries
    /// protocol traffic only; diagnostics go to stderr.
    /// </summary>
    public sealed class McpServeCommand : Command<McpServeCommand.Settings>
    {
        public sealed class Settings : CommandSettings { }

        public override int Execute(CommandContext context, Settings settings)
        {
            // The MCP protocol owns stdout — do NOT write anything else to it here.
            try
            {
                McpServerFactory.Create().Run();
                return 0;
            }
            catch (Exception ex)
            {
                System.Console.Error.WriteLine("mcp serve failed: " + ex.Message);
                return 1;
            }
        }
    }

    /// <summary>Lists the tools the MCP server exposes (a quick, human-readable inventory).</summary>
    public sealed class McpToolsCommand : Command<McpToolsCommand.Settings>
    {
        public sealed class Settings : CommandSettings { }

        public override int Execute(CommandContext context, Settings settings) => Runner.Guard(() =>
        {
            var db = McpServerFactory.LoadDatabase();
            var registry = McpServerFactory.BuildRegistry(db);
            var tools = registry.ToListJson();
            var table = new Table().AddColumn("Tool").AddColumn("Description");
            foreach (var t in tools)
                table.AddRow(Markup.Escape((string)t["name"]), Markup.Escape((string)t["description"]));
            AnsiConsole.Write(table);
            AnsiConsole.MarkupLineInterpolated($"[grey]instrument DB: {db.All.Count} model definition(s) loaded[/]");
            return 0;
        });
    }
}
