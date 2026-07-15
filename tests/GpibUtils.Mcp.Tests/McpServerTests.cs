using System.IO;
using System.Linq;
using GpibUtils.Mcp;
using GpibUtils.Mcp.Instruments;
using GpibUtils.Mcp.Protocol;
using GpibUtils.Mcp.Tools;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GpibUtils.Mcp.Tests
{
    /// <summary>
    /// Exercises the ported MCP server + instrument DB (issue #41): the bundled database loads, the tools
    /// drive the provider model, the JSON-RPC protocol round-trips, and a JSON <c>statusModel</c> block
    /// deserializes into the #43 completion engine's <see cref="GpibUtils.Visa.Srq.StatusModel"/>.
    /// </summary>
    public class McpServerTests
    {
        private static InstrumentDatabase Db() => McpServerFactory.LoadDatabase();

        private static ToolOutput Call(string name, JObject args)
        {
            var registry = McpServerFactory.BuildRegistry(Db());
            Assert.True(registry.TryGet(name, out var tool), $"tool '{name}' not registered");
            return tool.Invoke(args);
        }

        private static SimulatedGpibProvider SimProvider() => (SimulatedGpibProvider)GpibProviders.Get("Simulated");

        // ---- database ------------------------------------------------------

        [Fact]
        public void Bundled_database_loads_many_definitions()
        {
            Assert.True(Db().All.Count >= 40, "expected the bundled instrument DB to load (~55 models)");
        }

        [Fact]
        public void Db_get_returns_full_definition()
        {
            var text = Call("db_get", new JObject { ["model"] = "34401A" }).AsText();
            Assert.Contains("34401A", text);
            Assert.Contains("commands", text);
        }

        [Fact]
        public void Db_list_filters_by_search()
        {
            var text = Call("db_list", new JObject { ["search"] = "multimeter" }).AsText();
            Assert.Contains("34401A", text);
        }

        [Fact]
        public void Db_match_matches_an_idn_response()
        {
            var text = Call("db_match", new JObject { ["response"] = "HEWLETT-PACKARD,34401A,0,11-5-2" }).AsText();
            Assert.Contains("34401A", text);
        }

        [Fact]
        public void Json_status_model_deserializes_for_the_srq_engine()
        {
            // The 8563E definition carries a statusModel block — proof the DB is the #43 StatusModel source.
            Assert.True(Db().TryGet("8563E", out var def));
            Assert.NotNull(def.StatusModel);
            Assert.True(def.StatusModel.SrqSupported);
            Assert.Equal("requestService", def.StatusModel.RequestServiceBit);   // SRQ-edge flow
            Assert.True(def.StatusModel.Operations.ContainsKey("sweepComplete"));
        }

        [Fact]
        public void The_8563e_has_an_hpgl_capture_profile()
        {
            Assert.True(Db().TryGet("8563E", out var def));
            Assert.Equal("hpgl", def.Capture.Method);
            Assert.False(string.IsNullOrEmpty(def.Capture.PlotCommand));
        }

        // ---- provider-model tools ------------------------------------------

        [Fact]
        public void List_providers_includes_simulated()
        {
            Assert.Contains("Simulated", Call("list_providers", new JObject()).AsText());
        }

        [Fact]
        public void Query_tool_drives_the_simulated_provider()
        {
            const string resource = "GPIB0::5::INSTR";
            var sim = new SimulatedInstrument { IdentificationString = "ACME,MCP-TEST,0,1.0" };
            SimProvider().Add(resource, sim);

            var text = Call("query", new JObject
            {
                ["provider"] = "Simulated",
                ["resource"] = resource,
                ["command"] = "*IDN?"
            }).AsText();

            Assert.Contains("MCP-TEST", text);
        }

        [Fact]
        public void Srq_wait_runs_the_status_model_from_the_db()
        {
            // A plain simulated instrument never asserts the completion bit, so the #43 waiter times out —
            // but this exercises the whole path: DB lookup -> StatusModel -> CompletionWaiter -> session.
            const string resource = "GPIB0::18::INSTR";
            SimProvider().Add(resource, new SimulatedInstrument { IdentificationString = "HP8563E" });

            var text = Call("srq_wait", new JObject
            {
                ["provider"] = "Simulated",
                ["resource"] = resource,
                ["model"] = "8563E",
                ["operation"] = "sweepComplete",
                ["timeout_ms"] = 150
            }).AsText();

            Assert.Contains("TimedOut", text);
        }

        [Fact]
        public void Srq_wait_reports_missing_status_model()
        {
            // 34401A has no statusModel — srq_wait must say so rather than guess.
            var text = Call("srq_wait", new JObject
            {
                ["provider"] = "Simulated",
                ["resource"] = "GPIB0::22::INSTR",
                ["model"] = "34401A",
                ["operation"] = "whatever"
            }).AsText();
            Assert.Contains("no statusModel", text);
        }

        // ---- JSON-RPC protocol ---------------------------------------------

        private static JObject RoundTrip(params JObject[] requests)
        {
            var input = new StringReader(string.Join("\n", requests.Select(r => r.ToString(Newtonsoft.Json.Formatting.None))) + "\n");
            var output = new StringWriter();
            new McpServer(McpServerFactory.BuildRegistry(Db()), input, output).Run();
            // Return the LAST response line.
            var lines = output.ToString().Split(new[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
            return JObject.Parse(lines.Last());
        }

        [Fact]
        public void Initialize_returns_server_info()
        {
            var resp = RoundTrip(new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 1,
                ["method"] = "initialize",
                ["params"] = new JObject { ["protocolVersion"] = "2025-06-18" }
            });
            Assert.Equal("gpib-mcp", (string)resp["result"]["serverInfo"]["name"]);
        }

        [Fact]
        public void Tools_list_advertises_the_tools()
        {
            var resp = RoundTrip(new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 2,
                ["method"] = "tools/list"
            });
            var names = ((JArray)resp["result"]["tools"]).Select(t => (string)t["name"]).ToList();
            Assert.Contains("query", names);
            Assert.Contains("db_list", names);
            Assert.Contains("srq_wait", names);
            Assert.Contains("screen_capture", names);
        }

        [Fact]
        public void Tools_call_db_list_returns_content()
        {
            var resp = RoundTrip(new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 3,
                ["method"] = "tools/call",
                ["params"] = new JObject { ["name"] = "db_list", ["arguments"] = new JObject { ["search"] = "counter" } }
            });
            var content = (JArray)resp["result"]["content"];
            Assert.Equal("text", (string)content[0]["type"]);
            Assert.False((bool?)resp["result"]["isError"] ?? false);
        }

        [Fact]
        public void Unknown_tool_call_is_reported_as_error_result()
        {
            var resp = RoundTrip(new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 4,
                ["method"] = "tools/call",
                ["params"] = new JObject { ["name"] = "nonexistent_tool", ["arguments"] = new JObject() }
            });
            // Unknown tool name is an invalid-params JSON-RPC error.
            Assert.NotNull(resp["error"]);
        }
    }
}
