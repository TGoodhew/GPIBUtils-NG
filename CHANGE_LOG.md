# Changelog

All notable changes to **GPIBUtils-NG** are recorded here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); the project will adopt
[Semantic Versioning](https://semver.org/) once a first release is cut.

> **No-hardware build policy.** Development currently happens without access to the physical
> instruments. Code lands on `main` once it builds and passes the **Simulated**-provider unit tests;
> real-hardware confirmation is tracked separately and is **not** a merge gate. Entries below that
> touch an instrument driver are marked with their verification state:
> 🟡 **Verification Needed** (merged, awaiting bench) · ✅ **HW-Verified** (confirmed on real hardware).
> The live per-instrument status lives in [`docs/HARDWARE_VERIFICATION.md`](docs/HARDWARE_VERIFICATION.md).

## [Unreleased]

### Added

- **Rigol DS1054Z Oscilloscope** (issue [#27](https://github.com/TGoodhew/GPIBUtils-NG/issues/27), ported from
  [GPIBUtils](https://github.com/TGoodhew/GPIBUtils)) — the first driver in a new `GpibUtils.Instruments.Scopes`
  project (`IOscilloscope`). Acquisition control (`:RUN`/`:STOP`/`:SINGle`/`:AUToscale`), per-channel display
  (`:CHANnel{n}:DISPlay`), timebase readback, and automatic measurements (`:MEASure:ITEM? VPP|VMAX|FREQ,CHANnel{n}`).
  The DS1054Z is a USB/LXI instrument (no GPIB), so the default resource is the legacy app's LXI address — the
  transport-neutral provider model runs the same driver over any session. `RigolDs1054ZSimulatedDevice`
  (8 tests); `gpibutils ds1054z idn|acq|channel|measure` CLI branch. 🟡 **Verification Needed.**

- **HP 3325B Synthesizer / Function Generator** (issues [#28](https://github.com/TGoodhew/GPIBUtils-NG/issues/28) +
  [#29](https://github.com/TGoodhew/GPIBUtils-NG/issues/29), consolidated from two
  [GPIBUtils-Old](https://github.com/TGoodhew/GPIBUtils-Old) test apps — the 100 Hz harmonic/THD test and the
  DC-offset test) — a driver in `GpibUtils.Instruments.SignalSources`: waveform (`FU{n}` — DC/sine/square/
  triangle/positive-ramp), frequency (`FR` with HZ/KH/MH unit suffix), amplitude (`AM … VO`), DC offset
  (`OF … VO`), and amplitude calibration (`AC`). Configurable address (factory-default HP-IB address
  `GPIB0::17::INSTR` per the 3325B manual; both apps used a bench `::10::`). `Hp3325BSimulatedDevice`
  (8 tests); `gpibutils hp3325b idn|init|set` CLI branch. 🟡 **Verification Needed.**

- **HP 8350B Sweep Oscillator** (issue [#22](https://github.com/TGoodhew/GPIBUtils-NG/issues/22), ported from
  [GPIBUtils](https://github.com/TGoodhew/GPIBUtils)) — a write-only CW-source driver in
  `GpibUtils.Instruments.SignalSources`: preset (`IP`), CW frequency (`CW … MZ`), power level (`PL … DM`).
  The 8350B has no discrete RF on/off (RF is gated by the plug-in's leveling/blanking), so it is a faithful
  concrete driver rather than an `ISignalSource`. Configurable address (factory HP-IB address `GPIB0::19::INSTR`
  per the 8350B manual — note this shares the 8673B's factory 19, so remap one on a shared bench, as the 8340B
  is remapped to 20). `Hp8350BSimulatedDevice` (5 tests); `gpibutils hp8350b cw|freq|power|preset|init` CLI
  branch. 🟡 **Verification Needed.**

- **HP 5342A Microwave Frequency Counter** (issue [#32](https://github.com/TGoodhew/GPIBUtils-NG/issues/32),
  reconstructed from the 5342A manual — the [GPIBUtils-Old](https://github.com/TGoodhew/GPIBUtils-Old) source
  `.cs` was a mis-labelled DMM stub) — a driver in `GpibUtils.Instruments.Counters` for the microwave counter
  (Option 011 HP-IB): AUTO/MANUAL acquisition (`AU`/`MA`), manual center frequency (`SM…E`, integer MHz),
  resolution (`SR3`…`SR9`), and a talked frequency read guarding the over/under-level dashes sentinel.
  Bench address `GPIB0::2::INSTR` (manual programming examples; no fixed factory default — rear-panel switch).
  `Hp5342ASimulatedDevice` (10 tests); `gpibutils hp5342a idn|init|freq` CLI branch. Mnemonics reconstructed
  from the manual — flagged for bench confirmation. 🟡 **Verification Needed.**

- **HP 5351A Microwave Frequency Counter** (issue [#20](https://github.com/TGoodhew/GPIBUtils-NG/issues/20),
  ported from [GPIBUtils](https://github.com/TGoodhew/GPIBUtils)) — a driver in
  `GpibUtils.Instruments.Counters` for the single-input microwave counter: preset + clear SRQ mask
  (`SRQMASK,0`/`INIT`), sample-mode select (`SAMPLE,HOLD`/`FAST`), talked frequency read (Hz), and oven /
  reference status (`OVEN?`/`REF?`). Ported from the working `GPIBUtils/HPDevices` driver (its unwired
  frequency read is now implemented). Bench address `GPIB0::14::INSTR` (no fixed factory default — rear-panel
  switch). `Hp5351ASimulatedDevice` (7 tests); `gpibutils hp5351a idn|init|freq|status` CLI branch.
  🟡 **Verification Needed.**

- **HP/Agilent 3458A 8½-digit DMM** (issue [#31](https://github.com/TGoodhew/GPIBUtils-NG/issues/31), ported
  from [GPIBUtils-Old](https://github.com/TGoodhew/GPIBUtils-Old)) — a driver in
  `GpibUtils.Instruments.Meters` speaking the 3458A's native (non-SCPI) command language: `RESET`/`END ALWAYS`,
  `FUNC` (DCV/ACV/OHM/OHMF/DCI/ACI/FREQ/PER) with `SETACV SYNC` for AC volts, `NPLC`/`RES`, and single or
  burst triggered reads (`TARM SGL`). Identity via `ID?` (no `*IDN?`). Factory address `GPIB0::22::INSTR`
  (confirmed against the 3458A User's Guide). `Hp3458ASimulatedDevice` (11 tests);
  `gpibutils hp3458a idn|init|measure` CLI branch. 🟡 **Verification Needed.**

- **Rigol DM3058 Digital Multimeter** (issue [#26](https://github.com/TGoodhew/GPIBUtils-NG/issues/26),
  ported from [GPIBUtils](https://github.com/TGoodhew/GPIBUtils)) — a SCPI DMM in
  `GpibUtils.Instruments.Meters` (implements `IDigitalMultimeter`) driven with one-shot `MEASure:…?` queries
  across the function set (reusing `MeasurementFunction`). The **canonical** DM3058, superseding the older
  #30 implementation — and fixing its bug where AC current was mapped to the DC-current command. Configurable
  address (factory-default GPIB address `GPIB0::7::INSTR` per the DM3058 User's Guide; the app used LXI);
  `RigolDm3058SimulatedDevice` (11 tests); `gpibutils dm3058 idn|init|measure` CLI branch. 🟡 **Verification Needed.**

- **HP 438A RF Power Meter** (issue [#33](https://github.com/TGoodhew/GPIBUtils-NG/issues/33), ported from
  [GPIBUtils-Old](https://github.com/TGoodhew/GPIBUtils-Old)) — a pre-SCPI, mnemonic-driven `IPowerMeter` in
  `GpibUtils.Instruments.Meters` for the dual-channel (A/B) 438A: preset + Log-mode (`PR`/`LG`), sensor zero
  (`ZE`), cal-factor percent (`KB…PCT`), and per-channel power read (`{A|B}P TR2`) in dBm with the meter's
  over-range error sentinel surfaced as an exception. Factory address `GPIB0::13::INSTR` (confirmed against
  the 438A manual). `Hp438ASimulatedDevice` (10 tests); `gpibutils hp438a idn|init|zero|measure` CLI branch.
  The mnemonic set is reconstructed from a partial GUI app — flagged for bench confirmation. 🟡 **Verification Needed.**

- **HP/Agilent E4418B RF Power Meter** (issue [#25](https://github.com/TGoodhew/GPIBUtils-NG/issues/25),
  ported from [GPIBUtils](https://github.com/TGoodhew/GPIBUtils)) — an `IPowerMeter` in
  `GpibUtils.Instruments.Meters`. Zeroes+calibrates the sensor (`:CAL1:ALL`), applies the cal factor for a
  carrier frequency (`:FREQ …MHZ`), and measures power in dBm (`:CONF1;:INIT;FETCh?`). Both the cal and the
  measurement complete through the shared **#43 `CompletionWaiter`** OPC→SRQ handshake (`*ESE 1`/`*SRE 32`/
  `*OPC`) — the second driver on the engine after the 53131A. New shared `IPowerMeter` contract (also used by
  the HP 438A). `HpE4418BSimulatedDevice` (8 tests); `gpibutils e4418b idn|init|cal|measure` CLI branch.
  Bench address `GPIB0::13::INSTR`. 🟡 **Verification Needed.**

- **Rigol DP832 triple-output DC Power Supply** (issue [#15](https://github.com/TGoodhew/GPIBUtils-NG/issues/15),
  ported from [DP832](https://github.com/TGoodhew/DP832)) — a SCPI `IDcPowerSupply` in
  `GpibUtils.Instruments.PowerSupplies` with per-channel control of all three outputs (CH1/CH2 30 V, CH3 5 V):
  voltage / current limit (`:SOUR{n}:VOLT`/`:CURR`), output gating (`:OUTP CH{n}`), V/I/P measurement
  (`:MEAS:...? CH{n}`), and OVP/OCP. The `IDcPowerSupply` members act on a selectable channel; explicit
  per-channel overloads cover the rest. Configurable address (default GPIB address `GPIB0::2::INSTR` per the
  DP832 User's Guide; the app used `::1::`); `RigolDp832SimulatedDevice` (10 tests);
  `gpibutils dp832 idn|init|set|output|measure` CLI branch (#45). 🟡 **Verification Needed.**

- **HP/Agilent E3633A DC Power Supply** (issue [#19](https://github.com/TGoodhew/GPIBUtils-NG/issues/19),
  ported from [E3633A-Demo](https://github.com/TGoodhew/E3633A-Demo)) — first driver in a new
  `GpibUtils.Instruments.PowerSupplies` project (`IDcPowerSupply`). Sets output voltage / current limit
  (`VOLT`/`CURR`), gates the output (`OUTP`), reads back measured voltage/current (`MEAS:VOLT?`/`MEAS:CURR?`),
  and configures over-voltage protection. Configurable address (factory-default GPIB address `GPIB0::5::INSTR`,
  confirmed against the E3633A User's Guide — the demo used a bench `::27::`); `HpE3633ASimulatedDevice` for
  hardware-free testing (11 tests); `gpibutils hpe3633a idn|init|set|output|measure` CLI branch (#45).
  🟡 **Verification Needed.**

- **HP 53131A Universal Counter** (issues [#21](https://github.com/TGoodhew/GPIBUtils-NG/issues/21) +
  [#5](https://github.com/TGoodhew/GPIBUtils-NG/issues/5), ported from
  [GPIBUtils](https://github.com/TGoodhew/GPIBUtils) and [HP3499Demo](https://github.com/TGoodhew/HP3499Demo))
  — the **canonical** 53131A in a new `GpibUtils.Instruments.Counters` project (`IFrequencyCounter`),
  deduping the two identical `HPDevices` copies and the SCPI reader in HP3499Demo. Measures frequency on
  channels 1–3 (`CONF:FREQ (@n)`) and sets the channel-1 input impedance (50 Ω / 1 MΩ). Its measurement
  completion — the IEEE-488.2 `*ESE 1` / `*SRE 32` / `INIT;*OPC` → SRQ handshake — is driven through the
  shared **#43 `CompletionWaiter`** (direct-bit flow, `*SRE {mask}`) via `SessionStatusChannel` rather than
  a hand-rolled serial-poll loop; the data-driven `StatusModel` is the only device-specific completion
  knowledge and can move to the #41 instrument DB unchanged. A completion timeout surfaces as a typed
  `Hp53131AException`. Configurable address (factory-default GPIB address `GPIB0::3::INSTR`, confirmed
  against the 53131A Programming Guide — the legacy demo used a bench `::23::`); `Hp53131ASimulatedDevice`
  for hardware-free testing (18 tests); `gpibutils hp53131a idn|init|reset|freq` CLI branch (#45).
  🟡 **Verification Needed.**

- **HP 3499A Switch/Control System** (issue [#4](https://github.com/TGoodhew/GPIBUtils-NG/issues/4), ported
  from [HP3499Demo](https://github.com/TGoodhew/HP3499Demo)) — a plain-SCPI switch mainframe driver in
  `GpibUtils.Instruments.Switches`. Opens/closes relay channels on the `snn` (slot + two-digit channel)
  addressing scheme (`ROUT:CLOS`/`ROUT:OPEN`/`ROUT:CLOS? (@snn)`) and enumerates installed plug-in cards
  (`SYST:CTYPE?`) — the 44472A VHF and 44476B microwave switches are plug-ins addressed via this mainframe
  scheme, not separate instruments. Configurable address (factory-default GPIB address `GPIB0::9::INSTR`,
  confirmed against the 3499A User's & Programming Guide, matching the source); `Hp3499ASimulatedDevice`
  for hardware-free testing (17 tests); `gpibutils hp3499a idn|init|cards|close|open|state` CLI branch (#45).
  🟡 **Verification Needed.**

- **HP/Agilent/Keysight 34401A Digital Multimeter** (issues
  [#36](https://github.com/TGoodhew/GPIBUtils-NG/issues/36) + [#17](https://github.com/TGoodhew/GPIBUtils-NG/issues/17),
  ported from [5440Controller](https://github.com/TGoodhew/5440Controller) and
  [HP435B-Test](https://github.com/TGoodhew/HP435B-Test)) — the **canonical** 34401A DMM in
  `GpibUtils.Instruments.Meters`, a plain SCPI `IDigitalMultimeter`. Exposes the full measurement surface:
  configure any function (DCV/ACV/DCI/ACI/2W+4W resistance/frequency/period/continuity/diode) with
  range+resolution, tune SENSe (NPLC / autorange / input impedance / autozero), TRIGger + SAMPle
  (source/count/delay), CALCulate math (null / dB / dBm / limits / average-statistics) and DISPlay text;
  read one value or a burst (`READ?`/`FETCh?`) with `DmmStatistics` (min/max/avg/sample-σ) summarizing the
  burst — consolidating the buffered recorder-output acquisition from HP435B-Test with the rich SCPI menu
  from 5440Controller. Configurable address (factory-default GPIB address `GPIB0::22::INSTR`, confirmed
  against the 34401A User's Guide); `Hp34401ASimulatedDevice` for hardware-free testing (32 tests);
  `gpibutils hp34401a idn|init|reset|read|measure|stats|selftest|errors|display` CLI branch (#45).
  🟡 **Verification Needed.**

- **Per-instrument GPIB address configuration** (issue
  [#54](https://github.com/TGoodhew/GPIBUtils-NG/issues/54)) — the bench's *actual* addresses can now be
  set and persisted, instead of being hardcoded constants or passed on every command line. A shared
  `InstrumentAddressStore` (in `GpibUtils.Common`, JSON at `%APPDATA%\GpibUtils\addresses.json` or
  `$GPIBUTILS_CONFIG`, framework-native serialization — no NuGet dependency) maps device → resource, and
  address resolution now follows the precedence **`--address` &gt; configured &gt; `DefaultResource`** (each
  driver's `DefaultResource` stays the documented manual factory default fallback). New Console commands:
  `gpibutils config address list | get <dev> | set <dev> <resource> | clear <dev>` and `config path`.
  Shared by design so the WPF shell can surface the same store once it grows per-instrument panels (11 tests).

- **`GpibUtils.Wpf`** — the Windows front-end: a WPF **MVVM** shell (issue
  [#1](https://github.com/TGoodhew/GPIBUtils-NG/issues/1)) sitting on the same shared core as the console.
  Lists the registered providers and their capabilities, discovers instruments, and runs a command against
  any provider — works with no hardware via the Simulated provider. `ViewModelBase` / `RelayCommand` MVVM
  primitives; view-model logic is unit-tested headlessly.
- **Continuous integration** — GitHub Actions workflow (`.github/workflows/ci.yml`) building and testing the
  whole solution on `windows-latest` with **no NI-VISA installed**, on every push/PR to `main`.
- **`GpibUtils.Hpgl` / `GpibUtils.Mcp`** — scaffold projects completing the foundation solution layout;
  filled in by their migrations (#42 and #41).
- **SRQ / serial-poll completion engine** (`GpibUtils.Visa.Srq`, issue
  [#43](https://github.com/TGoodhew/GPIBUtils-NG/issues/43), ported from
  [GPIB-MCP](https://github.com/TGoodhew/GPIB-MCP)) — the shared, data-driven IEEE-488.2 completion state
  machine every SRQ-based driver now builds on, so no driver hand-rolls its own serial-poll/SemaphoreSlim
  handshake. A `StatusModel` (loadable from the instrument DB) declares the status-byte bits, enable-mask
  commands and named operations; `CompletionWaiter` runs the ARM→(busy)→confirm→restore flow, choosing the
  robust **SRQ-edge** strategy when the model names a request-service bit (hardware-confirmed on the 8563E)
  or the legacy **direct-bit** poll otherwise. Decoupled from the wire via `IStatusChannel`;
  `SessionStatusChannel` bridges it onto a live `IInstrumentSession`, and an injected clock/sleep makes it
  fully headless — driven end-to-end against a virtual-clock 8560-series simulator (11 new tests). Timeouts
  and per-model `BusyConfirmMs` are kept generous to tolerate HP-IB bus-extender latency. Targets
  SRQ-enable-mask-driven completions (e.g. 8560-series sweeps); instruments with their own settled-read
  handshake (e.g. the HP 8902A) keep it.
- **HP 8902A Measuring Receiver** (issue [#9](https://github.com/TGoodhew/GPIBUtils-NG/issues/9), ported
  from [HP-Attenuator](https://github.com/TGoodhew/HP-Attenuator)) — first `GpibUtils.Instruments.Meters`
  driver (`IMeasuringReceiver`). The **canonical** 8902A, consolidating the HP8902Measurements / GPIBUtils /
  GPIBUtils-Old implementations. Measures attenuation as relative Tuned RF Level (dB), absolute RF power
  (dBm) and signal frequency, with Normal + Frequency-Offset cal-factor tables, zero/sensor-cal, Track Mode
  (32.9SP), Average/Synchronous IF detectors, and a settled-read Data-Ready serial-poll completion handshake
  that surfaces UNCAL/RECAL and error-sentinel readings as typed `Hp8902AException`s. Configurable address
  (factory-default HP-IB address `GPIB0::14::INSTR`); `Hp8902ASimulatedDevice` for hardware-free testing
  (21 tests); `gpibutils hp8902a init|preset|status|frequency|power|level` CLI branch (#45).
  🟡 **Verification Needed.**

### Changed

- **`GpibUtils.Visa.Ni` now degrades gracefully without NI-VISA.** When the official NI/IVI VISA.NET
  assemblies aren't present at build time, the project compiles an "NI-VISA unavailable" provider stub
  (reported via the registry) instead of failing the build. The whole solution — including `Console` and
  `Wpf` — now builds and tests on a machine with **zero NI setup** (and on CI). Pass `-p:RequireNi=true`
  to hard-fail when NI is expected. No more "remove this project reference on a non-NI machine".

- **HP 8673B Synthesized Signal Generator** (issue [#8](https://github.com/TGoodhew/GPIBUtils-NG/issues/8),
  ported from [HP-Attenuator](https://github.com/TGoodhew/HP-Attenuator)) — second `GpibUtils.Instruments.SignalSources`
  driver, an `ILocalOscillator` (2–26.5 GHz) used as the LO for the 11793A converter path: preset (`IP`),
  frequency (`FR … MZ`), level (`LE … DM`), RF on/off. Configurable address (default `GPIB0::19::INSTR`);
  `Hp8673BSimulatedDevice` for hardware-free testing; `gpibutils hp8673b cw|freq|power|rf|preset|init`
  CLI branch (#45). The **canonical** 8673B driver, consolidating the implementations in HP8902Measurements,
  HP8273BLLMTest and GPIBUtils. 🟡 **Verification Needed.**
- **`GpibUtils.Instruments.SignalSources`** — signal-source driver category with a shared `ISignalSource`
  contract. **HP 8340B Synthesized Sweeper** (issue [#7](https://github.com/TGoodhew/GPIBUtils-NG/issues/7),
  ported from [HP-Attenuator](https://github.com/TGoodhew/HP-Attenuator)) driven as a CW source: preset
  (`IP`), frequency (`CW … MZ`), power (`PL … DB`), and RF on/off (`RF1`/`RF0`), with culture-invariant
  formatting. Runs on the shared `IInstrumentSession`; address configurable (default `GPIB0::20::INSTR`).
  Includes `Hp8340BSimulatedDevice` for hardware-free testing. This is the **canonical** 8340B driver for
  the related app issues [#16](https://github.com/TGoodhew/GPIBUtils-NG/issues/16) /
  [#34](https://github.com/TGoodhew/GPIBUtils-NG/issues/34) to build on. 🟡 **Verification Needed.**
- **`gpibutils hp8340b …` CLI branch** (issue [#45](https://github.com/TGoodhew/GPIBUtils-NG/issues/45))
  — `cw` (one-shot preset+freq+power+RF-on) / `freq` / `power` / `rf` / `preset` / `init`.
- **`GpibUtils.Instruments.Switches`** — first instrument driver category. **HP 11713A Attenuator/Switch
  Driver** (issue [#6](https://github.com/TGoodhew/GPIBUtils-NG/issues/6), ported from
  [HP-Attenuator](https://github.com/TGoodhew/HP-Attenuator)): dB→A/B relay-string solver
  (`Hp11713ACommandBuilder`), configurable attenuator wiring (`AttenuatorConfig`, default 8494+8496 =
  0–121 dB in 1 dB steps), independent S9/S0 switches, and a software state shadow (the 11713A is
  listen-only). Runs on the shared vendor-neutral `IInstrumentSession`; GPIB address is configurable
  (default `GPIB0::28::INSTR`). Includes `Hp11713ASimulatedDevice`, an in-memory model that decodes the
  A/B data strings back into relay state for hardware-free testing. 🟡 **Verification Needed.**
- **`gpibutils hp11713a …` CLI branch** (issue [#45](https://github.com/TGoodhew/GPIBUtils-NG/issues/45))
  — hierarchical, self-documenting commands `set` / `engage` / `zero` / `switch9` / `switch0` / `raw`,
  with shared `--provider` / `--address` / `--timeout` options (plus `--config` / `--invert-sense`) at
  the leaf. Establishes the per-instrument CLI pattern for future drivers.
- **`SimulatedInstrument.WriteObserver`** (foundation) — a hook that reports every write, so a simulated
  **listen-only** instrument can track the state it is driven into. Enables end-to-end,
  no-hardware verification of write-only drivers.

Foundation (issue [#1](https://github.com/TGoodhew/GPIBUtils-NG/issues/1)) — scaffolding and the
shared transport:

### Added (foundation)

- **`GpibUtils.Visa`** — vendor-neutral, pluggable GPIB transport (`net472`): `IGpibProvider` /
  `IInstrumentSession` abstractions, capability reporting, the `GpibProviders` registry, extension
  stubs (`Keysight-VISA` / `Prologix` / `AR488`) and an in-memory `Simulated` provider. Has **no
  vendor dependency, so it builds anywhere**. Provider selection via code or the
  `GPIBUTILS_GPIB_PROVIDER` env var (default `NI-VISA`).
- **`GpibUtils.Visa.Ni`** — `NI-VISA` (default) and native `NI-488.2` (opt-in
  `-p:DefineConstants=NI4882`) providers built against the **official NI / IVI VISA.NET assemblies**
  referenced by `HintPath` from the local NI-VISA install (never vendored; auto-registered by
  reflection when deployed).
- **`GpibUtils.Common`** — shared helpers, starting with a consolidated and hardened
  `ToEngineeringFormat`.
- **`GpibUtils.Console`** — runnable `Spectre.Console.Cli` app `gpibutils` with base commands
  `providers` / `discover` / `query` / `idn`; `-e <unit>` engineering-formats numeric replies. Runs
  hardware-free against the `Simulated` provider.
- **Tests** — xUnit suites for `GpibUtils.Visa` and `GpibUtils.Common` (**21 tests** green: 12 Visa +
  9 Common).
- **Docs** — [`docs/implementing-a-gpib-provider.md`](docs/implementing-a-gpib-provider.md) provider-authoring
  guide; `SharedMemory.md` portable project-status handoff.

### Project decisions

- **Language:** C# (other languages allowed in subprojects); primary language enforced via
  `.gitattributes` (Linguist) vendoring bulk data.
- **Target framework:** .NET Framework 4.7.2 (`net472`) for all projects, including the WPF front-end.
  Built with the .NET 10 SDK via `Microsoft.NETFramework.ReferenceAssemblies` (no full targeting pack
  required).
- **VISA layer:** pluggable provider model; NI providers use the **official NI assemblies** via
  `HintPath` (the Kelary community NuGet was dropped; the official NI NuGet is net6.0-only and
  unusable on `net472`).
- **UI split:** Console = Spectre.Console, Windows = WPF; the core/driver libraries carry no UI
  dependency. WinForms sources migrate to WPF.
- **Reference architecture:** the [HP-Attenuator](https://github.com/TGoodhew/HP-Attenuator) repo.
- **CLI-first (issue [#45](https://github.com/TGoodhew/GPIBUtils-NG/issues/45)):** every instrument
  must be fully driveable from a hierarchical CLI with self-documenting `--help` at every level.

[Unreleased]: https://github.com/TGoodhew/GPIBUtils-NG/commits/main
