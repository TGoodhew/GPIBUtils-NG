using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using GpibUtils.Hpgl;
using GpibUtils.Mcp.Instruments;
using GpibUtils.Mcp.Protocol;
using GpibUtils.Visa;
using GpibUtils.Visa.Srq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static GpibUtils.Mcp.Tools.ToolArgs;

namespace GpibUtils.Mcp.Tools
{
    /// <summary>
    /// Builds the MCP tools that expose the GPIBUtils-NG suite to an LLM client. Every instrument-driving
    /// tool runs over the vendor-neutral provider model (<see cref="GpibProviders"/> / <see cref="IInstrumentSession"/>),
    /// so it works against NI-VISA, a Prologix/AR488 adapter, or the in-memory simulator; the database tools
    /// read the shared <see cref="InstrumentDatabase"/>; <c>srq_wait</c> drives the #43 completion engine from
    /// a model's <see cref="StatusModel"/>; <c>screen_capture</c> renders an HP-GL plot via #42.
    /// </summary>
    public static class GpibTools
    {
        /// <summary>Registers all tools into <paramref name="registry"/>, backed by <paramref name="db"/>.</summary>
        public static ToolRegistry Register(ToolRegistry registry, InstrumentDatabase db)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            db = db ?? InstrumentDatabase.Empty();

            registry
                .Add(ListProviders())
                .Add(Discover())
                .Add(Query())
                .Add(Write())
                .Add(Read())
                .Add(Clear())
                .Add(Identify(db))
                .Add(DbList(db))
                .Add(DbGet(db))
                .Add(DbMatch(db))
                .Add(SrqWait(db))
                .Add(ScreenCapture(db));
            return registry;
        }

        // ---- session plumbing ----------------------------------------------

        private static IGpibProvider ResolveProvider(JObject args)
        {
            string name = Str(args, "provider", null);
            return string.IsNullOrWhiteSpace(name) ? GpibProviders.Default : GpibProviders.Get(name);
        }

        private static IInstrumentSession Open(JObject args)
        {
            var provider = ResolveProvider(args);
            string resource = ReqStr(args, "resource");
            int timeout = Int(args, "timeout_ms", 5000);
            return provider.Open(resource, new SessionSettings { TimeoutMilliseconds = timeout });
        }

        private static JProperty ResourceArg() => Required("resource", "string", "VISA resource string, e.g. GPIB0::18::INSTR.");
        private static JProperty ProviderArg() => Prop("provider", "string", "Provider name (e.g. NI-VISA, Simulated). Defaults to the registry default.");
        private static JProperty TimeoutArg() => Prop("timeout_ms", "integer", "I/O timeout in milliseconds (default 5000).");

        // ---- provider / transport tools ------------------------------------

        private static McpTool ListProviders() => new McpTool(
            "list_providers",
            "List the registered GPIB providers, their availability, and capabilities.",
            Schema(),
            args =>
            {
                var sb = new StringBuilder();
                foreach (var p in GpibProviders.All)
                {
                    var c = p.Capabilities;
                    sb.AppendLine($"{p.Name}{(string.Equals(p.Name, GpibProviders.DefaultProviderName, StringComparison.OrdinalIgnoreCase) ? " (default)" : "")}: " +
                                  (p.IsAvailable ? "available" : "unavailable" + (string.IsNullOrEmpty(p.UnavailableReason) ? "" : " — " + p.UnavailableReason)));
                    sb.AppendLine($"  discover={c.Discovery} serialPoll={c.SerialPoll} srq={c.ServiceRequest} clear={c.DeviceClear} local={c.ReturnToLocal} native={c.NativeAddressing}");
                }
                return sb.ToString().TrimEnd();
            });

        private static McpTool Discover() => new McpTool(
            "discover",
            "Discover instrument resources visible to a provider. WARNING: on a bench with HP-IB bus extenders a scan reports every address as present (all phantom) — drive by explicit resource instead.",
            Schema(ProviderArg(), Prop("filter", "string", "VISA resource filter (default ?*::INSTR).")),
            args =>
            {
                var provider = ResolveProvider(args);
                var list = provider.Discover(Str(args, "filter", "?*::INSTR"));
                var sb = new StringBuilder($"{list.Count} resource(s) via {provider.Name}:\n");
                foreach (var r in list) sb.AppendLine("  " + r);
                if (list.Count >= 15)
                    sb.AppendLine("\n[!] >=15 resources returned — an HP-IB bus extender is almost certainly in the path and this list is phantom. Do not trust it; drive instruments by explicit resource string.");
                return sb.ToString().TrimEnd();
            });

        private static McpTool Query() => new McpTool(
            "query",
            "Open a resource, send a command, and return the reply (write + read).",
            Schema(ResourceArg(), Required("command", "string", "Command to send, e.g. *IDN?."), ProviderArg(), TimeoutArg()),
            args =>
            {
                using (var s = Open(args))
                    return Clean(s.Query(ReqStr(args, "command")));
            });

        private static McpTool Write() => new McpTool(
            "write",
            "Open a resource and send a command (no reply expected).",
            Schema(ResourceArg(), Required("command", "string", "Command to send."), ProviderArg(), TimeoutArg()),
            args =>
            {
                using (var s = Open(args)) { s.Write(ReqStr(args, "command")); return "ok"; }
            });

        private static McpTool Read() => new McpTool(
            "read",
            "Open a resource and read a pending response.",
            Schema(ResourceArg(), ProviderArg(), TimeoutArg()),
            args =>
            {
                using (var s = Open(args)) return Clean(s.ReadString());
            });

        private static McpTool Clear() => new McpTool(
            "clear",
            "Send an IEEE-488.2 device clear to a resource.",
            Schema(ResourceArg(), ProviderArg(), TimeoutArg()),
            args =>
            {
                using (var s = Open(args)) { s.Clear(); return "cleared"; }
            });

        private static McpTool Identify(InstrumentDatabase db) => new McpTool(
            "identify",
            "Identify an instrument: send its identification query (from the model DB if a model is given, else *IDN?) and match the response against the database.",
            Schema(ResourceArg(), Prop("model", "string", "Optional model/alias to use its identity command instead of *IDN?."), ProviderArg(), TimeoutArg()),
            args =>
            {
                string idCmd = "*IDN?";
                string model = Str(args, "model", null);
                if (!string.IsNullOrWhiteSpace(model) && db.TryGet(model, out var def) && def.Identity?.Command != null)
                    idCmd = def.Identity.Command;

                string response;
                using (var s = Open(args)) response = Clean(s.Query(idCmd));

                var matches = db.MatchIdentity(response).Select(d => d.Model).ToList();
                var sb = new StringBuilder($"{idCmd} -> {response}");
                if (matches.Count > 0) sb.Append($"\nmatched model(s): {string.Join(", ", matches)}");
                return sb.ToString();
            });

        // ---- instrument database tools -------------------------------------

        private static McpTool DbList(InstrumentDatabase db) => new McpTool(
            "db_list",
            "List / search the instrument model database (by free text over model, manufacturer, category, description).",
            Schema(Prop("search", "string", "Optional case-insensitive substring filter."), Prop("category", "string", "Optional category filter (e.g. 'Digital Multimeter').")),
            args =>
            {
                string search = Str(args, "search", null);
                string category = Str(args, "category", null);
                IEnumerable<InstrumentDefinition> defs = db.All;
                if (!string.IsNullOrWhiteSpace(category))
                    defs = defs.Where(d => (d.Category ?? "").IndexOf(category, StringComparison.OrdinalIgnoreCase) >= 0);
                if (!string.IsNullOrWhiteSpace(search))
                    defs = defs.Where(d => Haystack(d).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);

                var list = defs.OrderBy(d => d.Model, StringComparer.OrdinalIgnoreCase).ToList();
                if (list.Count == 0) return "no matching instrument definitions.";
                var sb = new StringBuilder($"{list.Count} definition(s):\n");
                foreach (var d in list)
                    sb.AppendLine($"  {d.Model} — {d.Manufacturer} {d.Category} ({d.Commands?.Count ?? 0} commands)");
                return sb.ToString().TrimEnd();
            });

        private static McpTool DbGet(InstrumentDatabase db) => new McpTool(
            "db_get",
            "Get the full command reference for one instrument model (by model name or alias).",
            Schema(Required("model", "string", "Model name or alias, e.g. 34401A.")),
            args =>
            {
                if (!db.TryGet(ReqStr(args, "model"), out var def))
                    return $"no definition found for '{Str(args, "model", "")}'.";
                return JsonConvert.SerializeObject(def, Formatting.Indented);
            });

        private static McpTool DbMatch(InstrumentDatabase db) => new McpTool(
            "db_match",
            "Match an identification response (e.g. an *IDN? string) against the database and list the models whose identity pattern matches.",
            Schema(Required("response", "string", "The identification response to match.")),
            args =>
            {
                var matches = db.MatchIdentity(ReqStr(args, "response")).Select(d => d.Model).ToList();
                return matches.Count == 0 ? "no model matched." : "matched: " + string.Join(", ", matches);
            });

        // ---- SRQ completion (the #43 engine) -------------------------------

        private static McpTool SrqWait(InstrumentDatabase db) => new McpTool(
            "srq_wait",
            "Run a named completion operation from a model's statusModel (the shared #43 SRQ engine): arm the status mask, start the operation, and wait for operation-complete via serial poll.",
            Schema(ResourceArg(), Required("model", "string", "Model/alias whose statusModel defines the operation."),
                   Required("operation", "string", "The statusModel operation name, e.g. sweepComplete."),
                   Prop("timeout_ms", "integer", "Completion timeout in milliseconds (default 30000)."), ProviderArg()),
            args =>
            {
                string model = ReqStr(args, "model");
                string operation = ReqStr(args, "operation");
                int timeout = Int(args, "timeout_ms", 30000);
                if (!db.TryGet(model, out var def))
                    return $"no definition found for '{model}'.";
                if (def.StatusModel == null)
                    return $"'{model}' has no statusModel — cannot run an SRQ completion.";

                using (var s = Open(args))
                {
                    var channel = new SessionStatusChannel(s);
                    var sw = Stopwatch.StartNew();
                    var result = CompletionWaiter.Wait(def.StatusModel, model, operation, timeout,
                        channel, () => sw.ElapsedMilliseconds, Thread.Sleep);
                    return $"{result.Outcome}: {result.Message}";
                }
            });

        // ---- screen capture (the #42 renderer) -----------------------------

        private static McpTool ScreenCapture(InstrumentDatabase db) => new McpTool(
            "screen_capture",
            "Capture an instrument's screen via HP-GL plotter emulation and return it as a PNG image. Uses the model's capture profile (pre-roll, plot command, post-roll) from the database.",
            Schema(ResourceArg(), Required("model", "string", "Model/alias with an 'hpgl' capture profile."), ProviderArg(),
                   Prop("timeout_ms", "integer", "I/O timeout in milliseconds (default 30000 — plotting is slow).")),
            args =>
            {
                string model = ReqStr(args, "model");
                if (!db.TryGet(model, out var def) || def.Capture == null || !string.Equals(def.Capture.Method, "hpgl", StringComparison.OrdinalIgnoreCase))
                    return ToolOutput.Text($"'{model}' has no HP-GL capture profile.").AsError();

                if (Int(args, "timeout_ms", -1) < 0) args["timeout_ms"] = 30000;
                using (var s = Open(args))
                {
                    if (!string.IsNullOrWhiteSpace(def.Capture.PreRoll)) s.Write(def.Capture.PreRoll);
                    if (!string.IsNullOrWhiteSpace(def.Capture.PlotCommand)) s.Write(def.Capture.PlotCommand);
                    byte[] hpgl = s.ReadBytes();
                    if (!string.IsNullOrWhiteSpace(def.Capture.PostRoll)) s.Write(def.Capture.PostRoll);

                    if (hpgl == null || hpgl.Length == 0)
                        return ToolOutput.Text($"{model}: no HP-GL data returned from the plot command.").AsError();

                    byte[] png = HpglRenderer.RenderToPng(hpgl);
                    return ToolOutput.Image(png, "image/png", $"{model} screen capture ({hpgl.Length} HP-GL bytes)");
                }
            });

        private static string Haystack(InstrumentDefinition d) =>
            string.Join(" ", new[] { d.Model, d.Manufacturer, d.Category, d.Description });
    }
}
