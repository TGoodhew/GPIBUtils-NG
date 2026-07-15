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

                config.AddBranch<CommandSettings>("e4418b", dev =>
                {
                    dev.SetDescription("Drive an HP/Agilent E4418B RF power meter (SCPI, SRQ completion).");
                    dev.AddCommand<HpE4418BIdnCommand>("idn")
                        .WithDescription("Query the instrument identity (*IDN?).")
                        .WithExample(new[] { "e4418b", "idn", "--provider", "Simulated" });
                    dev.AddCommand<HpE4418BInitCommand>("init")
                        .WithDescription("Device clear + reset (clean known state).");
                    dev.AddCommand<HpE4418BCalCommand>("cal")
                        .WithDescription("Zero and calibrate the sensor.");
                    dev.AddCommand<HpE4418BMeasureCommand>("measure")
                        .WithDescription("Measure power (dBm) at a carrier frequency.")
                        .WithExample(new[] { "e4418b", "measure", "1000", "--provider", "Simulated" });
                });

                config.AddBranch<CommandSettings>("hp438a", dev =>
                {
                    dev.SetDescription("Drive an HP 438A dual-channel RF power meter (pre-SCPI mnemonics).");
                    dev.AddCommand<Hp438AIdnCommand>("idn")
                        .WithDescription("Show the instrument descriptor (438A has no *IDN?).");
                    dev.AddCommand<Hp438AInitCommand>("init")
                        .WithDescription("Device clear + preset + Log (dBm) mode.");
                    dev.AddCommand<Hp438AZeroCommand>("zero")
                        .WithDescription("Zero the sensor (ZE).");
                    dev.AddCommand<Hp438AMeasureCommand>("measure")
                        .WithDescription("Measure power (dBm) on channel A or B.")
                        .WithExample(new[] { "hp438a", "measure", "A", "--provider", "Simulated" });
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

                config.AddBranch<CommandSettings>("ds1054z", dev =>
                {
                    dev.SetDescription("Drive a Rigol DS1054Z oscilloscope (SCPI).");
                    dev.AddCommand<RigolDs1054ZIdnCommand>("idn")
                        .WithDescription("Query the instrument identity (*IDN?).")
                        .WithExample(new[] { "ds1054z", "idn", "--provider", "Simulated" });
                    dev.AddCommand<RigolDs1054ZAcqCommand>("acq")
                        .WithDescription("Acquisition control: run, stop, single, or auto.")
                        .WithExample(new[] { "ds1054z", "acq", "single", "--provider", "Simulated" });
                    dev.AddCommand<RigolDs1054ZChannelCommand>("channel")
                        .WithDescription("Turn a channel's display on or off.");
                    dev.AddCommand<RigolDs1054ZMeasureCommand>("measure")
                        .WithDescription("Measure vpp / vmax / freq on a channel.")
                        .WithExample(new[] { "ds1054z", "measure", "1", "--item", "vpp", "--provider", "Simulated" });
                });

                config.AddBranch<CommandSettings>("dm3058", dev =>
                {
                    dev.SetDescription("Drive a Rigol DM3058 digital multimeter (SCPI one-shot MEAS?).");
                    dev.AddCommand<RigolDm3058IdnCommand>("idn")
                        .WithDescription("Query the instrument identity (*IDN?).")
                        .WithExample(new[] { "dm3058", "idn", "--provider", "Simulated" });
                    dev.AddCommand<RigolDm3058InitCommand>("init")
                        .WithDescription("Device clear + *RST + *CLS (clean known state).");
                    dev.AddCommand<RigolDm3058MeasureCommand>("measure")
                        .WithDescription("Take a one-shot measurement of a function.")
                        .WithExample(new[] { "dm3058", "measure", "dcv", "--provider", "Simulated" });
                });

                config.AddBranch<CommandSettings>("hp3458a", dev =>
                {
                    dev.SetDescription("Drive an HP 3458A 8.5-digit DMM (native FUNC/NPLC/TARM language).");
                    dev.AddCommand<Hp3458AIdnCommand>("idn")
                        .WithDescription("Query the instrument identity (ID?).")
                        .WithExample(new[] { "hp3458a", "idn", "--provider", "Simulated" });
                    dev.AddCommand<Hp3458AInitCommand>("init")
                        .WithDescription("Device clear + RESET + END ALWAYS (clean known state).");
                    dev.AddCommand<Hp3458AMeasureCommand>("measure")
                        .WithDescription("Configure a function then read (or average a burst).")
                        .WithExample(new[] { "hp3458a", "measure", "dcv", "--nplc", "100", "--provider", "Simulated" });
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

                config.AddBranch<CommandSettings>("hpe3633a", dev =>
                {
                    dev.SetDescription("Drive an HP/Agilent E3633A DC power supply (SCPI).");
                    dev.AddCommand<HpE3633AIdnCommand>("idn")
                        .WithDescription("Query the instrument identity (*IDN?).")
                        .WithExample(new[] { "hpe3633a", "idn", "--provider", "Simulated" });
                    dev.AddCommand<HpE3633AInitCommand>("init")
                        .WithDescription("Device clear + *RST + *CLS (clean known state).");
                    dev.AddCommand<HpE3633ASetCommand>("set")
                        .WithDescription("Set output voltage (and optionally current limit / enable output).")
                        .WithExample(new[] { "hpe3633a", "set", "5", "--current", "1", "--on", "--provider", "Simulated" });
                    dev.AddCommand<HpE3633AOutputCommand>("output")
                        .WithDescription("Enable or disable the output (on/off).");
                    dev.AddCommand<HpE3633AMeasureCommand>("measure")
                        .WithDescription("Measure the actual output voltage and current.");
                });

                config.AddBranch<CommandSettings>("dp832", dev =>
                {
                    dev.SetDescription("Drive a Rigol DP832 triple-output DC power supply (SCPI).");
                    dev.AddCommand<RigolDp832IdnCommand>("idn")
                        .WithDescription("Query the instrument identity (*IDN?).")
                        .WithExample(new[] { "dp832", "idn", "--provider", "Simulated" });
                    dev.AddCommand<RigolDp832InitCommand>("init")
                        .WithDescription("Device clear + *RST + *CLS (clean known state).");
                    dev.AddCommand<RigolDp832SetCommand>("set")
                        .WithDescription("Set a channel's voltage (and optionally current / enable output).")
                        .WithExample(new[] { "dp832", "set", "5", "-c", "1", "-i", "1", "--on", "--provider", "Simulated" });
                    dev.AddCommand<RigolDp832OutputCommand>("output")
                        .WithDescription("Enable or disable a channel's output (on/off).");
                    dev.AddCommand<RigolDp832MeasureCommand>("measure")
                        .WithDescription("Measure a channel's voltage, current, and power.");
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

                config.AddBranch<CommandSettings>("hp5351a", dev =>
                {
                    dev.SetDescription("Drive an HP 5351A microwave frequency counter (mnemonic HP-IB).");
                    dev.AddCommand<Hp5351AIdnCommand>("idn")
                        .WithDescription("Show the instrument descriptor (5351A has no *IDN?).");
                    dev.AddCommand<Hp5351AInitCommand>("init")
                        .WithDescription("Device clear + clear SRQ mask + preset.");
                    dev.AddCommand<Hp5351AFreqCommand>("freq")
                        .WithDescription("Measure the input frequency (Hz).")
                        .WithExample(new[] { "hp5351a", "freq", "--provider", "Simulated" });
                    dev.AddCommand<Hp5351AStatusCommand>("status")
                        .WithDescription("Show oven and reference status.");
                });

                config.AddBranch<CommandSettings>("hp5342a", dev =>
                {
                    dev.SetDescription("Drive an HP 5342A microwave frequency counter (mnemonic HP-IB).");
                    dev.AddCommand<Hp5342AIdnCommand>("idn")
                        .WithDescription("Show the instrument descriptor (5342A has no *IDN?).");
                    dev.AddCommand<Hp5342AInitCommand>("init")
                        .WithDescription("Device clear + RESET + AUTO mode.");
                    dev.AddCommand<Hp5342AFreqCommand>("freq")
                        .WithDescription("Measure the input frequency (Hz); --center for manual mode.")
                        .WithExample(new[] { "hp5342a", "freq", "--center", "10000", "--provider", "Simulated" });
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

                config.AddBranch<CommandSettings>("hp8350b", dev =>
                {
                    dev.SetDescription("Drive an HP 8350B sweep oscillator as a CW source (write-only).");
                    dev.AddCommand<Hp8350BCwCommand>("cw")
                        .WithDescription("Preset, set CW frequency + power.")
                        .WithExample(new[] { "hp8350b", "cw", "7555", "-5", "--provider", "Simulated" });
                    dev.AddCommand<Hp8350BFreqCommand>("freq")
                        .WithDescription("Set the CW output frequency (MHz).");
                    dev.AddCommand<Hp8350BPowerCommand>("power")
                        .WithDescription("Set the output power (dBm).");
                    dev.AddCommand<Hp8350BPresetCommand>("preset")
                        .WithDescription("Instrument preset (IP).");
                    dev.AddCommand<Hp8350BInitCommand>("init")
                        .WithDescription("Device clear + preset (clean known state).");
                });

                config.AddBranch<CommandSettings>("hp3325b", dev =>
                {
                    dev.SetDescription("Drive an HP 3325B synthesizer / function generator (mnemonic HP-IB).");
                    dev.AddCommand<Hp3325BIdnCommand>("idn")
                        .WithDescription("Query the instrument identity (*IDN?).");
                    dev.AddCommand<Hp3325BInitCommand>("init")
                        .WithDescription("Device clear + *RST (clean known state).");
                    dev.AddCommand<Hp3325BSetCommand>("set")
                        .WithDescription("Set waveform / frequency (Hz) / amplitude (V) / DC offset (V).")
                        .WithExample(new[] { "hp3325b", "set", "-w", "sine", "-f", "1000", "-l", "1", "--provider", "Simulated" });
                });

                config.AddBranch<CommandSettings>("e4438c", dev =>
                {
                    dev.SetDescription("Drive a Keysight E4438C ESG vector RF signal generator (SCPI).");
                    dev.AddCommand<KeysightE4438CIdnCommand>("idn")
                        .WithDescription("Query the instrument identity (*IDN?).")
                        .WithExample(new[] { "e4438c", "idn", "--provider", "Simulated" });
                    dev.AddCommand<KeysightE4438CInitCommand>("init")
                        .WithDescription("Device clear + *RST + *CLS + RF off (clean known state).");
                    dev.AddCommand<KeysightE4438CCwCommand>("cw")
                        .WithDescription("Set frequency (MHz) + power (dBm) and turn the RF on.")
                        .WithExample(new[] { "e4438c", "cw", "1000", "-10", "--provider", "Simulated" });
                    dev.AddCommand<KeysightE4438CFreqCommand>("freq")
                        .WithDescription("Set the CW carrier frequency (MHz).");
                    dev.AddCommand<KeysightE4438CPowerCommand>("power")
                        .WithDescription("Set the output power (dBm).");
                    dev.AddCommand<KeysightE4438CRfCommand>("rf")
                        .WithDescription("Turn the RF output on or off.");
                    dev.AddCommand<KeysightE4438CModCommand>("mod")
                        .WithDescription("Enable or disable all modulation.");
                    dev.AddCommand<KeysightE4438CErrorCommand>("error")
                        .WithDescription("Read the head of the SCPI error queue (:SYSTem:ERRor?).");
                });

                config.AddBranch<CommandSettings>("fluke5440", dev =>
                {
                    dev.SetDescription("Drive a Fluke 5440A/5440B DC voltage calibrator (mnemonic HP-IB, no *IDN?).");
                    dev.AddCommand<Fluke5440IdnCommand>("idn")
                        .WithDescription("Show the instrument descriptor (5440 has no *IDN?).");
                    dev.AddCommand<Fluke5440FirmwareCommand>("firmware")
                        .WithDescription("Read the firmware version (GVRS).")
                        .WithExample(new[] { "fluke5440", "firmware", "--provider", "Simulated" });
                    dev.AddCommand<Fluke5440InitCommand>("init")
                        .WithDescription("Device clear + RESET (power-on state).");
                    dev.AddCommand<Fluke5440ResetCommand>("reset")
                        .WithDescription("Reset to the power-on state (RESET).");
                    dev.AddCommand<Fluke5440SetCommand>("set")
                        .WithDescription("Program the output voltage (SOUT); --operate to also go to Operate.")
                        .WithExample(new[] { "fluke5440", "set", "10", "--operate", "--provider", "Simulated" });
                    dev.AddCommand<Fluke5440GetCommand>("get")
                        .WithDescription("Read the present programmed output level (GOUT).");
                    dev.AddCommand<Fluke5440OperateCommand>("operate")
                        .WithDescription("Switch the output to Operate (OPER).");
                    dev.AddCommand<Fluke5440StandbyCommand>("standby")
                        .WithDescription("Switch the output to Standby (STBY).");
                    dev.AddCommand<Fluke5440SenseCommand>("sense")
                        .WithDescription("Select external 4-wire (ext) or internal 2-wire (int) sensing.")
                        .WithExample(new[] { "fluke5440", "sense", "ext", "--provider", "Simulated" });
                    dev.AddCommand<Fluke5440StatusCommand>("status")
                        .WithDescription("Read status / error / doing-state (GSTS / GERR / GONG).");
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
