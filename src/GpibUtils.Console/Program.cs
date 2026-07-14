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

                // Per-instrument GPIB address configuration (issue #54): persist the bench's actual
                // addresses so they override each driver's manual-default DefaultResource without editing
                // code or passing --address every time. Precedence: --address > configured > default.
                config.AddBranch<CommandSettings>("config", cfg =>
                {
                    cfg.SetDescription("View and persist per-instrument GPIB addresses.");
                    cfg.AddBranch<CommandSettings>("address", addr =>
                    {
                        addr.SetDescription("Configure the GPIB address used for each instrument (overrides the manual default).");
                        addr.AddCommand<ConfigAddressListCommand>("list")
                            .WithDescription("List every instrument's effective address and its source.");
                        addr.AddCommand<ConfigAddressGetCommand>("get")
                            .WithDescription("Show the effective address for one device.");
                        addr.AddCommand<ConfigAddressSetCommand>("set")
                            .WithDescription("Set (persist) a device's GPIB address.")
                            .WithExample(new[] { "config", "address", "set", "hp8340b", "GPIB0::20::INSTR" });
                        addr.AddCommand<ConfigAddressClearCommand>("clear")
                            .WithDescription("Remove a device's override (revert to the manual default).");
                    });
                    cfg.AddCommand<ConfigPathCommand>("path")
                        .WithDescription("Print the config-file path.");
                });

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

                config.AddBranch<CommandSettings>("hp8340b", dev =>
                {
                    dev.SetDescription("Drive an HP 8340B synthesized sweeper as a CW signal source.");
                    dev.AddCommand<Hp8340BCwCommand>("cw")
                        .WithDescription("Preset, set frequency + power, and turn the RF on.")
                        .WithExample(new[] { "hp8340b", "cw", "3000", "-10", "--provider", "Simulated" });
                    dev.AddCommand<Hp8340BFreqCommand>("freq")
                        .WithDescription("Set the CW output frequency (MHz).");
                    dev.AddCommand<Hp8340BPowerCommand>("power")
                        .WithDescription("Set the output power (dBm).");
                    dev.AddCommand<Hp8340BRfCommand>("rf")
                        .WithDescription("Turn the RF output on or off.");
                    dev.AddCommand<Hp8340BPresetCommand>("preset")
                        .WithDescription("Instrument preset (IP).");
                    dev.AddCommand<Hp8340BInitCommand>("init")
                        .WithDescription("Device clear + preset + RF off (clean known state).");
                });

                config.AddBranch<CommandSettings>("hp8902a", dev =>
                {
                    dev.SetDescription("Drive an HP 8902A measuring receiver (RF power / Tuned RF Level / frequency).");
                    dev.AddCommand<Hp8902AInitCommand>("init")
                        .WithDescription("Device clear + preset (clean known state).");
                    dev.AddCommand<Hp8902APresetCommand>("preset")
                        .WithDescription("Instrument preset (IP).");
                    dev.AddCommand<Hp8902AStatusCommand>("status")
                        .WithDescription("Serial-poll and print the status byte.");
                    dev.AddCommand<Hp8902AFrequencyCommand>("frequency")
                        .WithDescription("Measure the input signal frequency (MHz).")
                        .WithExample(new[] { "hp8902a", "frequency", "--provider", "Simulated" });
                    dev.AddCommand<Hp8902APowerCommand>("power")
                        .WithDescription("Measure absolute RF power (dBm) at a frequency.")
                        .WithExample(new[] { "hp8902a", "power", "3000", "--provider", "Simulated" });
                    dev.AddCommand<Hp8902ALevelCommand>("level")
                        .WithDescription("Measure the absolute Tuned RF Level (dBm) at a frequency.")
                        .WithExample(new[] { "hp8902a", "level", "12000", "--converted", "--lo", "17120.53", "--provider", "Simulated" });
                });

                config.AddBranch<CommandSettings>("hp34401a", dev =>
                {
                    dev.SetDescription("Drive an HP/Agilent/Keysight 34401A digital multimeter (SCPI).");
                    dev.AddCommand<Hp34401AIdnCommand>("idn")
                        .WithDescription("Query the instrument identity (*IDN?).")
                        .WithExample(new[] { "hp34401a", "idn", "--provider", "Simulated" });
                    dev.AddCommand<Hp34401AInitCommand>("init")
                        .WithDescription("Device clear + *RST + *CLS (clean known state).");
                    dev.AddCommand<Hp34401AResetCommand>("reset")
                        .WithDescription("Instrument reset (*RST).");
                    dev.AddCommand<Hp34401AReadCommand>("read")
                        .WithDescription("Read a single value from the current configuration (READ?).");
                    dev.AddCommand<Hp34401AMeasureCommand>("measure")
                        .WithDescription("Configure a function then read one value.")
                        .WithExample(new[] { "hp34401a", "measure", "dcv", "--range", "10", "--provider", "Simulated" });
                    dev.AddCommand<Hp34401AStatsCommand>("stats")
                        .WithDescription("Configure a function, take a burst, and report min/max/avg/sd.")
                        .WithExample(new[] { "hp34401a", "stats", "dcv", "-n", "100", "--provider", "Simulated" });
                    dev.AddCommand<Hp34401ASelfTestCommand>("selftest")
                        .WithDescription("Run the internal self-test (*TST?).");
                    dev.AddCommand<Hp34401AErrorsCommand>("errors")
                        .WithDescription("Drain and print the error queue (SYST:ERR?).");
                    dev.AddCommand<Hp34401ADisplayCommand>("display")
                        .WithDescription("Set or clear the front-panel display text.");
                });

                config.AddBranch<CommandSettings>("hp53131a", dev =>
                {
                    dev.SetDescription("Drive an HP 53131A universal frequency counter (SCPI, SRQ completion).");
                    dev.AddCommand<Hp53131AIdnCommand>("idn")
                        .WithDescription("Query the instrument identity (*IDN?).")
                        .WithExample(new[] { "hp53131a", "idn", "--provider", "Simulated" });
                    dev.AddCommand<Hp53131AInitCommand>("init")
                        .WithDescription("Device clear + reset + status preset (clean known state).");
                    dev.AddCommand<Hp53131AResetCommand>("reset")
                        .WithDescription("Instrument reset (*RST).");
                    dev.AddCommand<Hp53131AFreqCommand>("freq")
                        .WithDescription("Measure the frequency (Hz) on an input channel.")
                        .WithExample(new[] { "hp53131a", "freq", "1", "--impedance", "50", "--provider", "Simulated" });
                });

                config.AddBranch<CommandSettings>("hp3499a", dev =>
                {
                    dev.SetDescription("Drive an HP 3499A switch/control system (relay channels + card inventory).");
                    dev.AddCommand<Hp3499AIdnCommand>("idn")
                        .WithDescription("Query the instrument identity (*IDN?).")
                        .WithExample(new[] { "hp3499a", "idn", "--provider", "Simulated" });
                    dev.AddCommand<Hp3499AInitCommand>("init")
                        .WithDescription("Device clear + reset + status preset (clean known state).");
                    dev.AddCommand<Hp3499ACardsCommand>("cards")
                        .WithDescription("List the plug-in cards installed in each slot (SYSTem:CTYPE?).");
                    dev.AddCommand<Hp3499ACloseCommand>("close")
                        .WithDescription("Close a relay channel (snn: slot + channel).")
                        .WithExample(new[] { "hp3499a", "close", "100", "--provider", "Simulated" });
                    dev.AddCommand<Hp3499AOpenCommand>("open")
                        .WithDescription("Open a relay channel (snn: slot + channel).");
                    dev.AddCommand<Hp3499AStateCommand>("state")
                        .WithDescription("Query whether a relay channel is closed (ROUTe:CLOSe?).");
                });

                config.AddBranch<CommandSettings>("hp8673b", dev =>
                {
                    dev.SetDescription("Drive an HP 8673B synthesized signal generator (2-26.5 GHz), e.g. as an LO.");
                    dev.AddCommand<Hp8673BCwCommand>("cw")
                        .WithDescription("Preset, set frequency + level, and turn the RF on.")
                        .WithExample(new[] { "hp8673b", "cw", "10000", "8", "--provider", "Simulated" });
                    dev.AddCommand<Hp8673BFreqCommand>("freq")
                        .WithDescription("Set the output frequency (MHz).");
                    dev.AddCommand<Hp8673BPowerCommand>("power")
                        .WithDescription("Set the output level (dBm).");
                    dev.AddCommand<Hp8673BRfCommand>("rf")
                        .WithDescription("Turn the RF output on or off.");
                    dev.AddCommand<Hp8673BPresetCommand>("preset")
                        .WithDescription("Instrument preset (IP).");
                    dev.AddCommand<Hp8673BInitCommand>("init")
                        .WithDescription("Device clear + preset + RF off (clean known state).");
                });
            });
            return app.Run(args);
        }
    }
}
