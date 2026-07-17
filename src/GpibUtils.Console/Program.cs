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

                // Cross-instrument verification (issue #37): 5440 calibrator vs 34401A DMM.
                config.AddBranch<CommandSettings>("verify", v =>
                {
                    v.SetDescription("Cross-instrument verification runners.");
                    v.AddCommand<Verify5440Command>("5440")
                        .WithDescription("Verify a Fluke 5440 through a list of points, read back on a 34401A (exit 1 on any FAIL).")
                        .WithExample(new[] { "verify", "5440", "--points", "0,1,-1,10,-10", "--tolerance-ppm", "50", "--provider", "Simulated" });
                });

                // Attenuation-measurement app (issue #34): orchestrate source+LO+attenuator+receiver.
                config.AddBranch<CommandSettings>("measure", m =>
                {
                    m.SetDescription("End-to-end attenuation-vs-frequency measurement (8340B + 8673B + 11713A + 8902A).");
                    m.AddCommand<MeasureSweepCommand>("sweep")
                        .WithDescription("Sweep frequency + attenuation and print per-frequency error/depth.")
                        .WithExample(new[] { "measure", "sweep", "--start", "1000", "--stop", "2000", "--step", "500", "--provider", "Simulated" });
                });

                // HP-GL pen plotters (issues #38/#39/#40): stream a plot file to a 7090A/7475A/7550A.
                config.AddBranch<CommandSettings>("plotter", dev =>
                {
                    dev.SetDescription("Drive an HP 7090A/7475A/7550A HP-GL pen plotter (stream a plot; preview via #42).");
                    dev.AddCommand<HpPlotterIdnCommand>("idn")
                        .WithDescription("Show the plotter identity (OI?).")
                        .WithExample(new[] { "plotter", "idn", "--provider", "Simulated" });
                    dev.AddCommand<HpPlotterInitCommand>("init")
                        .WithDescription("Device clear + HP-GL initialize (IN).");
                    dev.AddCommand<HpPlotterPlotCommand>("plot")
                        .WithDescription("Stream an HP-GL plot file to the plotter; --preview writes a PNG too.")
                        .WithExample(new[] { "plotter", "plot", "drawing.plt", "-m", "7550a", "--preview", "drawing.png" });
                    dev.AddCommand<HpPlotterWindowCommand>("window")
                        .WithDescription("Read the hard-clip output window (OW) and scaling points (OP).");
                });

                // MCP server (issue #41): expose the suite to an LLM client over JSON-RPC/stdio.
                config.AddBranch<CommandSettings>("mcp", m =>
                {
                    m.SetDescription("Model Context Protocol server exposing the suite to an LLM client.");
                    m.AddCommand<McpServeCommand>("serve")
                        .WithDescription("Run the MCP JSON-RPC server over stdio (stdout = protocol only).");
                    m.AddCommand<McpToolsCommand>("tools")
                        .WithDescription("List the tools the MCP server exposes and the loaded model DB size.");
                });

                // HP-GL rendering utility (issue #42): render a captured plot file to PNG/SVG.
                config.AddBranch<CommandSettings>("hpgl", h =>
                {
                    h.SetDescription("HP-GL/2 plot utilities (render a captured plot file to an image).");
                    h.AddCommand<HpglRenderCommand>("render")
                        .WithDescription("Render an HP-GL/2 plot file (.plt) to PNG (default) or SVG.")
                        .WithExample(new[] { "hpgl", "render", "capture.plt", "-o", "capture.png" });
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

                config.AddBranch<CommandSettings>("hp8672a", dev =>
                {
                    dev.SetDescription("Drive an HP 8672A synthesized microwave signal generator (legacy program-code, #96 phase-lock settle).");
                    dev.AddCommand<Hp8672AInitCommand>("init")
                        .WithDescription("Device clear + RF off (resets to 3 GHz).");
                    dev.AddCommand<Hp8672ACwCommand>("cw")
                        .WithDescription("Set frequency (settle for phase lock) + power, and turn the RF on.")
                        .WithExample(new[] { "hp8672a", "cw", "12000", "-10", "--provider", "Simulated" });
                    dev.AddCommand<Hp8672AFreqCommand>("freq")
                        .WithDescription("Set the CW frequency (MHz) and wait for phase lock.");
                    dev.AddCommand<Hp8672APowerCommand>("power")
                        .WithDescription("Set the output power (dBm).");
                    dev.AddCommand<Hp8672ARfCommand>("rf")
                        .WithDescription("Turn the RF output on or off.");
                    dev.AddCommand<Hp8672AStatusCommand>("status")
                        .WithDescription("Serial-poll and report the status byte (phase-lock / fault bits).");
                });

                config.AddBranch<CommandSettings>("hp33120a", dev =>
                {
                    dev.SetDescription("Drive an HP 33120A function/arbitrary waveform generator (SCPI).");
                    dev.AddCommand<Hp33120AIdnCommand>("idn")
                        .WithDescription("Query the instrument identity (*IDN?).")
                        .WithExample(new[] { "hp33120a", "idn", "--provider", "Simulated" });
                    dev.AddCommand<Hp33120AInitCommand>("init")
                        .WithDescription("Device clear + *RST + Vpp units (clean known state).");
                    dev.AddCommand<Hp33120AApplyCommand>("apply")
                        .WithDescription("Set waveform/frequency/amplitude/offset (and output).")
                        .WithExample(new[] { "hp33120a", "apply", "-w", "sine", "-f", "1000", "-a", "2", "--provider", "Simulated" });
                });

                config.AddBranch<CommandSettings>("dg1000z", dev =>
                {
                    dev.SetDescription("Drive a Rigol DG1000Z function/arbitrary waveform generator (SCPI, dual channel).");
                    dev.AddCommand<Dg1000ZIdnCommand>("idn")
                        .WithDescription("Query the instrument identity (*IDN?).")
                        .WithExample(new[] { "dg1000z", "idn", "--provider", "Simulated" });
                    dev.AddCommand<Dg1000ZApplyCommand>("apply")
                        .WithDescription("Set waveform/frequency/amplitude/offset (and output) on a channel.")
                        .WithExample(new[] { "dg1000z", "apply", "-c", "1", "-w", "square", "-f", "1000", "--provider", "Simulated" });
                });

                config.AddBranch<CommandSettings>("hp8116a", dev =>
                {
                    dev.SetDescription("Drive an HP 8116A pulse/function generator (legacy mnemonics).");
                    dev.AddCommand<Hp8116AApplyCommand>("apply")
                        .WithDescription("Set frequency/amplitude/offset (and output). Waveform-select is bench-TBD (#118).")
                        .WithExample(new[] { "hp8116a", "apply", "-f", "1000", "-a", "2", "--output", "on", "--provider", "Simulated" });
                    dev.AddCommand<Hp8116AStatusCommand>("status")
                        .WithDescription("Serial-poll the status byte and read any error (IERR).");
                });

                // ISignalSource RF-generator batch (#103/#119/#120/#122/#123/#125/#137/#138): idn + apply.
                RegisterSignalSource<E4436BSourceSettings>(config, "e4436b", "Agilent E4436B ESG-D signal generator (SCPI).");
                RegisterSignalSource<Hp83620ASourceSettings>(config, "hp83620a", "HP 83620A synthesized swept-signal generator (SCPI, CW).");
                RegisterSignalSource<Hp83712BSourceSettings>(config, "hp83712b", "HP 83712B synthesized CW generator (SCPI).");
                RegisterSignalSource<Hp8656SourceSettings>(config, "hp8656", "HP 8656A/8656B signal generator (legacy mnemonic, write-only).");
                RegisterSignalSource<Hp8657BSourceSettings>(config, "hp8657b", "HP 8657B signal generator (legacy mnemonic, listen-only).");
                RegisterSignalSource<Hp8664ASourceSettings>(config, "hp8664a", "HP 8664A signal generator (HP-SL).");
                RegisterSignalSource<RsSmeSourceSettings>(config, "rs-sme", "Rohde & Schwarz SME signal generator (SCPI).");
                RegisterSignalSource<RsSmtSourceSettings>(config, "rs-smt", "Rohde & Schwarz SMT signal generator (SCPI).");

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

                config.AddBranch<CommandSettings>("hp5005b", dev =>
                {
                    dev.SetDescription("Drive an HP 5005B signature multimeter (legacy mnemonics, QM-mask/SRQ measurement).");
                    dev.AddCommand<Hp5005BIdnCommand>("idn")
                        .WithDescription("Query the instrument identity (ID).")
                        .WithExample(new[] { "hp5005b", "idn", "--provider", "Simulated" });
                    dev.AddCommand<Hp5005BInitCommand>("init")
                        .WithDescription("Device clear + reset to power-up defaults (clean known state).");
                    dev.AddCommand<Hp5005BMeasureCommand>("measure")
                        .WithDescription("Select a numeric function and read a measurement (QM/SRQ handshake).")
                        .WithExample(new[] { "hp5005b", "measure", "resistance", "--provider", "Simulated" });
                    dev.AddCommand<Hp5005BSignatureCommand>("signature")
                        .WithDescription("Capture a logic signature (NORM/--qual QUAL) via the SRQ handshake.");
                    dev.AddCommand<Hp5005BErrorCommand>("error")
                        .WithDescription("Read the decimal error code (SE).");
                });

                config.AddBranch<CommandSettings>("hp4275a", dev =>
                {
                    dev.SetDescription("Drive an HP 4275A multi-frequency LCR meter (legacy program-codes, I1/SRQ measurement).");
                    dev.AddCommand<Hp4275AIdnCommand>("idn")
                        .WithDescription("Show the instrument descriptor (4275A has no *IDN?).")
                        .WithExample(new[] { "hp4275a", "idn", "--provider", "Simulated" });
                    dev.AddCommand<Hp4275AInitCommand>("init")
                        .WithDescription("Device clear + HOLD/MANUAL trigger (clean known state).");
                    dev.AddCommand<Hp4275AMeasureCommand>("measure")
                        .WithDescription("Configure parameter/frequency/circuit and take one measurement (SRQ handshake).")
                        .WithExample(new[] { "hp4275a", "measure", "-p", "c", "-f", "100k", "--provider", "Simulated" });
                    dev.AddCommand<Hp4275AZeroOpenCommand>("zero-open")
                        .WithDescription("Perform an OPEN (zero) correction.");
                });

                config.AddBranch<CommandSettings>("hp8903b", dev =>
                {
                    dev.SetDescription("Drive an HP 8903B audio analyzer (legacy mnemonics, SF22/SRQ measurement).");
                    dev.AddCommand<Hp8903BIdnCommand>("idn")
                        .WithDescription("Show the instrument descriptor (8903B has no *IDN?).")
                        .WithExample(new[] { "hp8903b", "idn", "--provider", "Simulated" });
                    dev.AddCommand<Hp8903BInitCommand>("init")
                        .WithDescription("Device clear + Automatic Operation reset (clean known state).");
                    dev.AddCommand<Hp8903BMeasureCommand>("measure")
                        .WithDescription("Set source + measurement, trigger and read (SF22/SRQ handshake).")
                        .WithExample(new[] { "hp8903b", "measure", "-f", "1000", "-m", "distortion", "--provider", "Simulated" });
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

                config.AddBranch<CommandSettings>("hp8560e", dev =>
                {
                    dev.SetDescription("Drive an HP 8560E spectrum analyzer (mnemonic HP-IB, #43 SRQ sweep completion).");
                    dev.AddCommand<Hp8560EIdnCommand>("idn")
                        .WithDescription("Query the instrument identity (ID?).")
                        .WithExample(new[] { "hp8560e", "idn", "--provider", "Simulated" });
                    dev.AddCommand<Hp8560EInitCommand>("init")
                        .WithDescription("Device clear + preset + clear SRQ mask (clean known state).");
                    dev.AddCommand<Hp8560ESweepCommand>("sweep")
                        .WithDescription("Set center/span/RBW, take a single sweep (SRQ handshake); --peak reports the marker.")
                        .WithExample(new[] { "hp8560e", "sweep", "-c", "1000", "-s", "10", "--peak", "--provider", "Simulated" });
                    dev.AddCommand<Hp8560ETraceCommand>("trace")
                        .WithDescription("Read the current trace (TRA?) and print a summary.");
                    dev.AddCommand<Hp8560EPeakCommand>("peak")
                        .WithDescription("Place the marker on the peak and report frequency + amplitude.");
                });

                config.AddBranch<CommandSettings>("hp8591e", dev =>
                {
                    dev.SetDescription("Drive an HP 8591E spectrum analyzer (8590-family legacy mnemonics, RQS/STB? sweep completion).");
                    dev.AddCommand<Hp8591EIdnCommand>("idn")
                        .WithDescription("Query the instrument identity (ID?).")
                        .WithExample(new[] { "hp8591e", "idn", "--provider", "Simulated" });
                    dev.AddCommand<Hp8591EInitCommand>("init")
                        .WithDescription("Device clear + preset + clear SRQ mask (clean known state).");
                    dev.AddCommand<Hp8591ESweepCommand>("sweep")
                        .WithDescription("Set center/span/RBW, take a single sweep (RQS/STB? handshake); --peak reports the marker.")
                        .WithExample(new[] { "hp8591e", "sweep", "-c", "300", "-s", "20", "--peak", "--provider", "Simulated" });
                    dev.AddCommand<Hp8591ETraceCommand>("trace")
                        .WithDescription("Read the current trace (TRA?) and print a summary.");
                    dev.AddCommand<Hp8591EPeakCommand>("peak")
                        .WithDescription("Place the marker on the peak and report frequency + amplitude.");
                });

                config.AddBranch<CommandSettings>("hp3585", dev =>
                {
                    dev.SetDescription("Drive an HP 3585A/3585B spectrum analyzer (legacy mnemonics, CQ op-complete SRQ).");
                    dev.AddCommand<Hp3585IdnCommand>("idn")
                        .WithDescription("Show the instrument descriptor (3585 has no *IDN?).")
                        .WithExample(new[] { "hp3585", "idn", "--provider", "Simulated" });
                    dev.AddCommand<Hp3585InitCommand>("init")
                        .WithDescription("Device clear + preset + disable op-complete SRQ (clean known state).");
                    dev.AddCommand<Hp3585SweepCommand>("sweep")
                        .WithDescription("Set center/span/RBW, take a single sweep (CQ/serial-poll handshake); --peak reports the peak.")
                        .WithExample(new[] { "hp3585", "sweep", "-c", "10", "-s", "100", "--peak", "--provider", "Simulated" });
                    dev.AddCommand<Hp3585TraceCommand>("trace")
                        .WithDescription("Read the current trace (D3 dump) and print a summary.");
                    dev.AddCommand<Hp3585MarkerCommand>("marker")
                        .WithDescription("Read the marker frequency + amplitude (D2 / D1 dumps).");
                });

                config.AddBranch<CommandSettings>("hp85620a", dev =>
                {
                    dev.SetDescription("Drive an HP 85620A mass memory module through an 8563E (catalog / card store-load / DLPs).");
                    dev.AddCommand<Hp85620AIdnCommand>("idn")
                        .WithDescription("Show the host analyzer identity (ID?).")
                        .WithExample(new[] { "hp85620a", "idn", "--provider", "Simulated" });
                    dev.AddCommand<Hp85620ACatalogCommand>("catalog")
                        .WithDescription("Catalog a storage device (mem or card): list entries + free bytes.")
                        .WithExample(new[] { "hp85620a", "catalog", "card", "--provider", "Simulated" });
                    dev.AddCommand<Hp85620AStoreCommand>("store")
                        .WithDescription("Store a named module entry onto the card (CARDSTORE).");
                    dev.AddCommand<Hp85620ALoadCommand>("load")
                        .WithDescription("Load a named entry from the card into module memory (CARDLOAD).");
                    dev.AddCommand<Hp85620AClearCommand>("clear")
                        .WithDescription("Dispose all entries in module memory (DISPOSE ALL).");
                    dev.AddCommand<Hp85620ADecodeCommand>("decode")
                        .WithDescription("Offline: decode a raw SRAM dump and list/export the stored DLPs (no GPIB).")
                        .WithExample(new[] { "hp85620a", "decode", "SRAM_85620A.bin", "--out", "dlps" });
                });

                config.AddBranch<CommandSettings>("e4406a", dev =>
                {
                    dev.SetDescription("Drive an Agilent E4406A VSA transmitter tester (SCPI, Basic mode).");
                    dev.AddCommand<AgilentE4406AIdnCommand>("idn")
                        .WithDescription("Query the instrument identity (*IDN?).")
                        .WithExample(new[] { "e4406a", "idn", "--provider", "Simulated" });
                    dev.AddCommand<AgilentE4406AInitCommand>("init")
                        .WithDescription("Device clear + *RST + *CLS + Basic single mode (clean known state).");
                    dev.AddCommand<AgilentE4406AChPowerCommand>("chpower")
                        .WithDescription("Measure channel power (dBm) + PSD at a center frequency (MHz).")
                        .WithExample(new[] { "e4406a", "chpower", "1000", "-s", "5", "--provider", "Simulated" });
                    dev.AddCommand<AgilentE4406AAcpCommand>("acp")
                        .WithDescription("Measure adjacent channel power at a center frequency (MHz).");
                    dev.AddCommand<AgilentE4406AMeasureCommand>("measure")
                        .WithDescription("Run a raw measurement by root (CHPower/ACP/PSTatistic/WAVeform/SPECtrum).")
                        .WithExample(new[] { "e4406a", "measure", "SPECtrum", "-c", "1000", "--provider", "Simulated" });
                    dev.AddCommand<AgilentE4406AErrorCommand>("error")
                        .WithDescription("Read the head of the SCPI error queue (:SYSTem:ERRor?).");
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

        /// <summary>Registers an <c>idn</c> + <c>apply</c> CLI branch for an ISignalSource RF generator.</summary>
        private static void RegisterSignalSource<TSettings>(IConfigurator config, string key, string description)
            where TSettings : SignalSourceSettings
        {
            config.AddBranch<CommandSettings>(key, dev =>
            {
                dev.SetDescription(description);
                dev.AddCommand<SignalSourceApplyCommand<TSettings>>("apply")
                    .WithDescription("Set frequency/level (and RF on/off).")
                    .WithExample(new[] { key, "apply", "-f", "1000", "-l", "-10", "--rf", "on", "--provider", "Simulated" });
            });
        }
    }
}
