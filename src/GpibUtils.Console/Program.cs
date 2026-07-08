using Spectre.Console.Cli;

namespace GpibUtils.Console
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            // Render Spectre's box-drawing/Unicode correctly on Windows consoles (no-op if redirected).
            try { System.Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }

            var app = new CommandApp();
            app.Configure(config =>
            {
                config.SetApplicationName("gpibutils");
                config.AddCommand<ProvidersCommand>("providers")
                      .WithDescription("List the registered GPIB providers and their capabilities.");
                config.AddCommand<DiscoverCommand>("discover")
                      .WithDescription("Discover instruments visible to a provider.")
                      .WithExample(new[] { "discover", "--provider", "Simulated" });
                config.AddCommand<QueryCommand>("query")
                      .WithDescription("Open a resource, send a command, and print the reply.")
                      .WithExample(new[] { "query", "GPIB0::5::INSTR", "*IDN?", "--provider", "Simulated" });
                config.AddCommand<IdnCommand>("idn")
                      .WithDescription("Query *IDN? from an instrument.")
                      .WithExample(new[] { "idn", "GPIB0::5::INSTR", "-p", "Simulated" });
            });
            return app.Run(args);
        }
    }
}
