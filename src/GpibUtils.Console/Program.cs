using GpibUtils.Console.Instruments;
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

                // Instrument command branches (issue #45): every device is fully driveable from the CLI.
                // The branch itself carries no options; each leaf owns the shared instrument options
                // (provider/address/timeout/…) so they parse in the natural trailing position,
                // e.g. `gpibutils hp11713a set 30 --provider Simulated`.
                config.AddBranch<CommandSettings>("hp11713a", dev =>
                {
                    dev.SetDescription("Drive an HP 11713A attenuator/switch driver (listen-only; state is software-tracked).");
                    dev.AddCommand<Hp11713ASetCommand>("set")
                        .WithDescription("Set total attenuation in dB.")
                        .WithExample(new[] { "hp11713a", "set", "30", "--provider", "Simulated" });
                    dev.AddCommand<Hp11713AEngageCommand>("engage")
                        .WithDescription("Engage specific section digits (1-8), bypass the rest.");
                    dev.AddCommand<Hp11713AZeroCommand>("zero")
                        .WithDescription("Set 0 dB (bypass all sections).");
                    dev.AddCommand<Hp11713ASwitch9Command>("switch9")
                        .WithDescription("Drive independent switch S9 (on = A9, off = B9).");
                    dev.AddCommand<Hp11713ASwitch0Command>("switch0")
                        .WithDescription("Drive independent switch S0 (on = A0, off = B0).");
                    dev.AddCommand<Hp11713ARawCommand>("raw")
                        .WithDescription("Send a raw 11713A data string.");
                });
            });
            return app.Run(args);
        }
    }
}
