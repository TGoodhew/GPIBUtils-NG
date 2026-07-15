using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GpibUtils.Mcp.Instruments;
using GpibUtils.Mcp.Protocol;
using GpibUtils.Mcp.Tools;

namespace GpibUtils.Mcp
{
    /// <summary>
    /// Assembles the GPIBUtils-NG MCP server: loads the bundled instrument-model database, registers the
    /// GPIB tools over the provider model, and builds an <see cref="McpServer"/> on a chosen stdio pair.
    /// </summary>
    public static class McpServerFactory
    {
        /// <summary>Loads the instrument database from the bundled <c>data/instruments</c> folder(s) plus the
        /// user override directory (later dirs win, per <see cref="InstrumentDatabase"/> merge rules).</summary>
        public static InstrumentDatabase LoadDatabase() =>
            InstrumentDatabase.Load(CandidateDataDirs());

        /// <summary>Builds the full tool registry over <paramref name="db"/>.</summary>
        public static ToolRegistry BuildRegistry(InstrumentDatabase db) =>
            GpibTools.Register(new ToolRegistry(), db);

        /// <summary>Builds a server that speaks JSON-RPC over the given reader/writer (defaults to stdio).</summary>
        public static McpServer Create(TextReader input = null, TextWriter output = null)
        {
            var db = LoadDatabase();
            return new McpServer(BuildRegistry(db), input ?? Console.In, output ?? Console.Out);
        }

        /// <summary>The directories searched for bundled/user instrument definitions, de-duplicated.</summary>
        private static IEnumerable<string> CandidateDataDirs()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dir in Raw())
                if (!string.IsNullOrWhiteSpace(dir) && seen.Add(dir))
                    yield return dir;
        }

        private static IEnumerable<string> Raw()
        {
            yield return Path.Combine(AppContext.BaseDirectory ?? ".", "data", "instruments");
            var asmDir = Path.GetDirectoryName(typeof(McpServerFactory).Assembly.Location);
            if (!string.IsNullOrEmpty(asmDir)) yield return Path.Combine(asmDir, "data", "instruments");
            yield return InstrumentPaths.UserDatabaseDir();
        }
    }
}
