# GPIBUtils-NG

**Next-generation, consolidated GPIB/VISA instrument-control suite.**

This repository is the migration target for the collection of individual instrument-control
projects scattered across [@TGoodhew](https://github.com/TGoodhew)'s repos. Each existing
instrument driver, application, and utility that manipulates a GPIB / HP-IB / VISA test
instrument is tracked here as its own **migration issue** (one issue per instrument/driver).
The goal is a single solution with reusable drivers, a shared VISA transport, a common app/CLI
shell, and an MCP server surface — replacing ~18 one-off repos.

> **Status: migration complete; bench verification is what remains.** Every driver found in the surveyed
> repos — and every programmable instrument turned up by the manuals triage — has been ported: **68 drivers**
> across 20 instrument categories, plus the HP-GL/PCL renderers, the MCP server, the SRQ completion engine,
> the attenuation-measurement application and the cross-instrument verification harness. `main` builds and
> passes **781 tests** hardware-free.
>
> Because development happens away from the bench, drivers merge on simulator/unit-test green and then wait
> for real-hardware confirmation. See [`CHANGE_LOG.md`](CHANGE_LOG.md) for what's landed and
> [`docs/HARDWARE_VERIFICATION.md`](docs/HARDWARE_VERIFICATION.md) for the verification board.

## Why consolidate

The surveyed repos share a very consistent stack (C# / .NET Framework 4.7.2–4.8.1, Spectre.Console
or WinForms/WPF, NI-VISA) but each re-implements the same plumbing: VISA session management, SRQ /
serial-poll completion, engineering-notation formatting, and — in four separate places — a driver
for the **HP 8673B**, and in four places a driver for the **HP 8902A**. Consolidation removes that
duplication and standardizes on one clean, testable, vendor-neutral core.

## Quick start

Build the solution and run the Spectre.Console front-end (`gpibutils`). The **Simulated** provider
needs no hardware, so this works anywhere:

```
dotnet build GPIBUtils-NG.sln
```

The built executable is **`gpibutils.exe`** (the assembly is named `gpibutils`, not `GpibUtils.Console`).
It is **not** placed on `PATH`, so invoke it by path — from the repo root:

```powershell
& "src\GpibUtils.Console\bin\Release\net472\gpibutils.exe" providers

# Talk to a simulated instrument (no hardware)
& "src\GpibUtils.Console\bin\Release\net472\gpibutils.exe" idn GPIB0::5::INSTR --provider Simulated
& "src\GpibUtils.Console\bin\Release\net472\gpibutils.exe" query GPIB0::14::INSTR "MEAS:VOLT?" --provider Simulated --engineering V

# Interactive TUI (menu- and dashboard-driven; also the default with no verb on a terminal)
& "src\GpibUtils.Console\bin\Release\net472\gpibutils.exe" tui --provider Simulated

# Against real hardware (NI-VISA is the default provider)
& "src\GpibUtils.Console\bin\Release\net472\gpibutils.exe" idn GPIB0::14::INSTR
```

> **Don't use `discover` to find instruments on a bench behind HP-IB bus extenders.** An extender ACKs the
> address handshake for its whole remote segment, so a VISA scan reports *every* address 0–30 as present —
> all phantom. Drive by explicit resource string (`--address`), or persist the real addresses once with
> `config address set <device> <resource>`. Both front-ends warn when a scan returns ≥ 15 resources.

## Target architecture

| Layer | Project | Responsibility |
|---|---|---|
| Transport (core) | `GpibUtils.Visa` | Vendor-neutral pluggable transport: `IGpibProvider` / `IInstrumentSession`, capability reporting, SRQ/serial-poll, the provider registry, extension stubs (Keysight/Prologix/AR488), and an in-memory simulator. **No vendor dependency — builds anywhere.** |
| Transport (NI) | `GpibUtils.Visa.Ni` | NI-VISA (default) + native NI-488.2 providers, built against the **official NI/IVI VISA.NET assemblies** from the local NI-VISA install (referenced by path, never vendored; auto-registered by reflection). Where NI-VISA isn't installed it builds an *"NI-VISA unavailable"* stub, so the whole solution still compiles with zero NI setup (and on CI). |
| Shared | `GpibUtils.Common` | Cross-cutting helpers — `ToEngineeringFormat`, and `InstrumentAddressStore` (per-instrument addresses persisted to JSON; precedence `--address` > configured > driver default) |
| Drivers | `GpibUtils.Instruments.*` | One driver class per instrument model, grouped by category: SignalSources, Meters, Counters, Analyzers, Scopes, PowerSupplies, Switches, Plotters, Printers, Calibrators, Audio, LcrMeters, ElectronicLoads, ModulationDomain, NetworkAnalyzers, NoiseFigureMeters, SourceMeasure |
| Rendering | `GpibUtils.Hpgl` · `GpibUtils.Pcl` | HP-GL/2 parser + renderer (plotters, analyzer screen-capture) and the ThinkJet-subset PCL parser + raster renderer |
| Output routing | `GpibUtils.Hardcopy` | Routes one document (HP-GL / PCL / image) to a GPIB plotter, the GPIB ThinkJet, or a Windows printer |
| Measurement app | `GpibUtils.Measurement` | Attenuation-vs-frequency orchestration — `MeasurementEngine` + the 11793A LO/IF planner; drives source + LO + step attenuators + receiver |
| Automation | `GpibUtils.Verification` | Cross-instrument verification harness: verify a DUT with a **selectable** reference instrument (e.g. an 8902A *or* a power meter to check a signal generator). Plan-driven runners + `verify harness` / `verify source`, drivable hardware-free via the simulated bench. See [`docs/VERIFICATION_HARNESS.md`](docs/VERIFICATION_HARNESS.md) |
| Integration | `GpibUtils.Mcp` | MCP server exposing the suite to LLM clients + a **55-model** instrument DB |
| Front-end (console) | `GpibUtils.Console` | **Spectre.Console** — both a one-shot CLI (`Spectre.Console.Cli`) and an interactive **TUI** (`gpibutils tui`) |
| Front-end (Windows) | `GpibUtils.Wpf` | **WPF** MVVM desktop shell on the shared core — browse providers, run a command, DMM panel, hardcopy preview/send (works hardware-free via the Simulated provider) |

The **core/driver libraries carry no UI dependencies** — the console (Spectre.Console CLI + TUI) and Windows (WPF) front-ends are the only UI projects, and all call the same drivers/services. WinForms sources (ESG-SignalCreator, SALink) migrate to WPF.

**UI parity is a hard rule.** Nothing may exist in one front-end that isn't available in all three (CLI · WPF · TUI). The only accepted asymmetry is *invocation*: the CLI is one-shot, so an interactive/live screen maps to a verb (a live dashboard ↔ a streaming `monitor`/`watch`; a transcript ↔ scrollback plus `--log`). The underlying capability must exist everywhere.

**Reference implementation:** the [HP-Attenuator](https://github.com/TGoodhew/HP-Attenuator) repo
already follows the intended pattern (vendor-neutral `Ivi.Visa`, a `Core` library, a hardware
simulator, and CI) and is the model the rest should converge on.

**Language & target framework (decided):** C# on **.NET Framework 4.7.2** (`net472`) for all
projects, including the WPF front-end — matching the bulk of the existing source and the
HP-Attenuator reference for zero VISA.NET churn. See #1.

## Development workflow (build-forward, verify-on-hardware)

Much of this work is done **without access to the physical instruments**, so building and
hardware verification are deliberately decoupled:

- **`main`** is the always-green integration line — it must build and pass the **Simulated**-provider
  unit tests. Everything stacks on it.
- Work each issue on a **`feat/<issue#>-<slug>`** branch (e.g. `feat/6-hp11713a`): port the driver onto
  the shared transport, add a simulator/mock and tests, and wire its CLI branch (#45).
- A PR **merges to `main` on simulator/unit-test green — hardware verification is _not_ a merge gate.**
  This lets the next driver build on it immediately while away from the bench.
- On merge the issue **is closed** (policy changed 2026-07-17), so the open issue list only shows work still
  to *build*. It gets the **`Needs Verification`** label and a bench checklist, a row on the board, and the
  merge commit is tagged **`verify/<issue#>-<instrument>`** so its exact state can be checked out at the
  bench later. The full board is [`docs/HARDWARE_VERIFICATION.md`](docs/HARDWARE_VERIFICATION.md) (mirrored
  by the pinned tracking issue [#46](https://github.com/TGoodhew/GPIBUtils-NG/issues/46), which stays open).
- Back at the bench, run the checklist against real hardware and record the result on the board. On a ✅ pass
  mark it verified; on a ⚠️ discrepancy **reopen** the issue or file a follow-up.
- **Software-only work has no bench gate** — pure infrastructure (the SRQ engine, the renderers, the MCP
  server, address config) is fully verified by its unit tests and is simply closed on merge.

Changes are logged in [`CHANGE_LOG.md`](CHANGE_LOG.md).

## Migration issue format

Every migration issue follows the same template so the work is uniform:

- **Source** — link to the origin repo, the item type (driver / application / utility / mcp-server), and the key source files.
- **What the code does** — the instrument-facing behavior being preserved.
- **Current stack** — language/framework, bus (GPIB / HP-IB / SCPI / VISA), and VISA/IO layer.
- **Migration notes** — hardcoded addresses, legacy COM, HintPath issues, quirks to fix.
- **Related implementations** — cross-links to duplicate drivers of the same instrument to be consolidated.
- **Target in GPIBUtils-NG** — the destination module.
- **Migration checklist** — port logic, move to shared transport, de-hardcode addressing, reconcile duplicates, add simulator/tests, wire into CLI/MCP, docs.

Labels: `migration` (all), plus `driver` / `application` / `utility` / `mcp-server`,
`bus:gpib` / `bus:scpi`, `stack:dotnet` / `stack:python` / `stack:vbnet`, `legacy`, `needs-triage`.

## Migration backlog

**42 instrument/driver items** were found across the surveyed repos (plus the **#1** foundation issue). The
table below is the **original survey**, kept for provenance — every item in it has since been migrated.

A second discovery pass ([#70](https://github.com/TGoodhew/GPIBUtils-NG/issues/70)) then triaged all **571
PDFs** in the manuals library and migrated a driver for every programmable instrument that wasn't already
backlogged, taking the total to **68 drivers**. That auditable triage is committed at
[`docs/manuals-triage.md`](docs/manuals-triage.md); the live status of every driver is on
[`docs/HARDWARE_VERIFICATION.md`](docs/HARDWARE_VERIFICATION.md).


### Meters (DMM / power / measuring receiver)

| # | Instrument / component | Source repo | Type | Target module |
|---|---|---|---|---|
| [#2](https://github.com/TGoodhew/GPIBUtils-NG/issues/2) | HP 8902A Measuring Receiver | [HP8902Measurements](https://github.com/TGoodhew/HP8902Measurements) | application | `GpibUtils.Instruments.Meters (HP8902A) + app shell` |
| [#9](https://github.com/TGoodhew/GPIBUtils-NG/issues/9) | HP 8902A Measuring Receiver | [HP-Attenuator](https://github.com/TGoodhew/HP-Attenuator) | driver | `GpibUtils.Instruments.Meters (HP8902A — canonical)` |
| [#17](https://github.com/TGoodhew/GPIBUtils-NG/issues/17) | HP 34401A Digital Multimeter | [HP435B-Test](https://github.com/TGoodhew/HP435B-Test) | application | `GpibUtils.Instruments.Meters (HP34401A) + report generator` |
| [#24](https://github.com/TGoodhew/GPIBUtils-NG/issues/24) | HP 8902A Measuring Receiver | [GPIBUtils](https://github.com/TGoodhew/GPIBUtils) | driver | `GpibUtils.Instruments.Meters (HP8902A)` |
| [#25](https://github.com/TGoodhew/GPIBUtils-NG/issues/25) | HP/Agilent E4418B Power Meter | [GPIBUtils](https://github.com/TGoodhew/GPIBUtils) | driver | `GpibUtils.Instruments.Meters (E4418B)` |
| [#26](https://github.com/TGoodhew/GPIBUtils-NG/issues/26) | Rigol DM3058 Digital Multimeter (current version) | [GPIBUtils](https://github.com/TGoodhew/GPIBUtils) | application | `GpibUtils.Instruments.Meters (RigolDM3058) + logging` |
| [#30](https://github.com/TGoodhew/GPIBUtils-NG/issues/30) | Rigol DM3058 Digital Multimeter (original) | [GPIBUtils-Old](https://github.com/TGoodhew/GPIBUtils-Old) _(legacy)_ | application | `GpibUtils.Instruments.Meters (RigolDM3058 — use GPIBUtils version)` |
| [#31](https://github.com/TGoodhew/GPIBUtils-NG/issues/31) | HP 3458A 8.5-digit DMM (capture/logging) | [GPIBUtils-Old](https://github.com/TGoodhew/GPIBUtils-Old) _(legacy)_ | application | `GpibUtils.Instruments.Meters (HP3458A) + logging` |
| [#33](https://github.com/TGoodhew/GPIBUtils-NG/issues/33) | HP 438A Power Meter | [GPIBUtils-Old](https://github.com/TGoodhew/GPIBUtils-Old) _(legacy)_ | application | `GpibUtils.Instruments.Meters (HP438A)` |
| [#36](https://github.com/TGoodhew/GPIBUtils-NG/issues/36) | HP/Agilent/Keysight 34401A DMM (calibrator verification) | [5440Controller](https://github.com/TGoodhew/5440Controller) | application | `GpibUtils.Instruments.Meters (HP34401A — canonical)` |


### RF / signal sources

| # | Instrument / component | Source repo | Type | Target module |
|---|---|---|---|---|
| [#3](https://github.com/TGoodhew/GPIBUtils-NG/issues/3) | HP 8673B Synthesized Signal Generator (as LO) | [HP8902Measurements](https://github.com/TGoodhew/HP8902Measurements) | application | `GpibUtils.Instruments.SignalSources (HP8673B)` |
| [#7](https://github.com/TGoodhew/GPIBUtils-NG/issues/7) | HP 8340B Synthesized Sweeper | [HP-Attenuator](https://github.com/TGoodhew/HP-Attenuator) | driver | `GpibUtils.Instruments.SignalSources (HP8340B)` |
| [#8](https://github.com/TGoodhew/GPIBUtils-NG/issues/8) | HP 8673B Synthesized Signal Generator | [HP-Attenuator](https://github.com/TGoodhew/HP-Attenuator) | driver | `GpibUtils.Instruments.SignalSources (HP8673B)` |
| [#11](https://github.com/TGoodhew/GPIBUtils-NG/issues/11) | Keysight/Agilent E4438C ESG (RF vector signal generator) | [ESG-SignalCreator](https://github.com/TGoodhew/ESG-SignalCreator) | application | `GpibUtils.Instruments.SignalSources (E4438C) + Arb encoder in Core` |
| [#16](https://github.com/TGoodhew/GPIBUtils-NG/issues/16) | HP 8340A / 8340B Synthesized Sweep Generator | [HP8340ACalVerification](https://github.com/TGoodhew/HP8340ACalVerification) | application | `GpibUtils.Instruments.SignalSources (HP8340) + cal/verify app` |
| [#18](https://github.com/TGoodhew/GPIBUtils-NG/issues/18) | HP 8673B Synthesized Signal Generator (repo mislabeled '8273B') | [HP8273BLLMTest](https://github.com/TGoodhew/HP8273BLLMTest) | driver | `GpibUtils.Instruments.SignalSources (HP8673B)` |
| [#22](https://github.com/TGoodhew/GPIBUtils-NG/issues/22) | HP 8350B Sweep Oscillator | [GPIBUtils](https://github.com/TGoodhew/GPIBUtils) | driver | `GpibUtils.Instruments.SignalSources (HP8350B)` |
| [#23](https://github.com/TGoodhew/GPIBUtils-NG/issues/23) | HP 8673B Synthesized Signal Generator | [GPIBUtils](https://github.com/TGoodhew/GPIBUtils) | driver | `GpibUtils.Instruments.SignalSources (HP8673B)` |
| [#28](https://github.com/TGoodhew/GPIBUtils-NG/issues/28) | HP 3325B Synthesizer — 100 Hz Harmonic/THD test | [GPIBUtils-Old](https://github.com/TGoodhew/GPIBUtils-Old) _(legacy)_ | application | `GpibUtils.Instruments.SignalSources (HP3325B) + THD test app` |
| [#29](https://github.com/TGoodhew/GPIBUtils-NG/issues/29) | HP 3325B Synthesizer — DC Offset test | [GPIBUtils-Old](https://github.com/TGoodhew/GPIBUtils-Old) _(legacy)_ | application | `GpibUtils.Instruments.SignalSources (HP3325B) + DC-offset test` |


### Frequency counters

| # | Instrument / component | Source repo | Type | Target module |
|---|---|---|---|---|
| [#5](https://github.com/TGoodhew/GPIBUtils-NG/issues/5) | HP 53131A Universal Frequency Counter | [HP3499Demo](https://github.com/TGoodhew/HP3499Demo) | application | `GpibUtils.Instruments.Counters (HP53131A)` |
| [#20](https://github.com/TGoodhew/GPIBUtils-NG/issues/20) | HP 5351A Microwave Frequency Counter | [GPIBUtils](https://github.com/TGoodhew/GPIBUtils) | driver | `GpibUtils.Instruments.Counters (HP5351A)` |
| [#21](https://github.com/TGoodhew/GPIBUtils-NG/issues/21) | HP 53131A Universal Counter | [GPIBUtils](https://github.com/TGoodhew/GPIBUtils) | driver | `GpibUtils.Instruments.Counters (HP53131A — canonical)` |
| [#32](https://github.com/TGoodhew/GPIBUtils-NG/issues/32) | HP 5342A Microwave Frequency Counter | [GPIBUtils-Old](https://github.com/TGoodhew/GPIBUtils-Old) _(legacy)_ | driver | `GpibUtils.Instruments.Counters (HP5342A)` |


### Spectrum analyzers & memory modules

| # | Instrument / component | Source repo | Type | Target module |
|---|---|---|---|---|
| [#10](https://github.com/TGoodhew/GPIBUtils-NG/issues/10) | HP/Agilent 8563E Spectrum Analyzer + 85620A Mass Memory Module (FRAM card) | [MemCardTest](https://github.com/TGoodhew/MemCardTest) | utility | `GpibUtils.Instruments.Analyzers (HP8560series + MassMemory)` |
| [#12](https://github.com/TGoodhew/GPIBUtils-NG/issues/12) | Agilent E4406A VSA Transmitter Tester | [ESG-SignalCreator](https://github.com/TGoodhew/ESG-SignalCreator) | application | `GpibUtils.Instruments.Analyzers (E4406A) + Verify harness` |
| [#13](https://github.com/TGoodhew/GPIBUtils-NG/issues/13) | HP/Agilent 8560E Spectrum Analyzer | [DLPBits](https://github.com/TGoodhew/DLPBits) | application | `GpibUtils.Instruments.Analyzers (HP8560series)` |
| [#14](https://github.com/TGoodhew/GPIBUtils-NG/issues/14) | HP 85620A Mass Memory Module (DLP loader / SRAM image decode) | [DLPBits](https://github.com/TGoodhew/DLPBits) | utility | `GpibUtils.Instruments.Analyzers (MassMemory + image decode)` |


### Power supplies

| # | Instrument / component | Source repo | Type | Target module |
|---|---|---|---|---|
| [#15](https://github.com/TGoodhew/GPIBUtils-NG/issues/15) | Rigol DP832 Programmable DC Power Supply | [DP832](https://github.com/TGoodhew/DP832) | application | `GpibUtils.Instruments.PowerSupplies (RigolDP832)` |
| [#19](https://github.com/TGoodhew/GPIBUtils-NG/issues/19) | HP/Agilent E3633A DC Power Supply | [E3633A-Demo](https://github.com/TGoodhew/E3633A-Demo) | application | `GpibUtils.Instruments.PowerSupplies (E3633A)` |


### Switch / attenuator control

| # | Instrument / component | Source repo | Type | Target module |
|---|---|---|---|---|
| [#4](https://github.com/TGoodhew/GPIBUtils-NG/issues/4) | HP 3499A Switch/Control System | [HP3499Demo](https://github.com/TGoodhew/HP3499Demo) | application | `GpibUtils.Instruments.Switches (HP3499A)` |
| [#6](https://github.com/TGoodhew/GPIBUtils-NG/issues/6) | HP/Agilent 11713A Attenuator/Switch Driver | [HP-Attenuator](https://github.com/TGoodhew/HP-Attenuator) | application | `GpibUtils.Instruments.Switches (HP11713A)` |


### Plotters (HP-GL)

| # | Instrument / component | Source repo | Type | Target module |
|---|---|---|---|---|
| [#38](https://github.com/TGoodhew/GPIBUtils-NG/issues/38) | HP 7090A Measurement Plotting System | [7090ATest](https://github.com/TGoodhew/7090ATest) | application | `GpibUtils.Instruments.Plotters (HP7090A) + GpibUtils.Hpgl` |
| [#39](https://github.com/TGoodhew/GPIBUtils-NG/issues/39) | HP 7550A Graphics Plotter | [7550ATest](https://github.com/TGoodhew/7550ATest) | application | `GpibUtils.Instruments.Plotters (HP7550A) + GpibUtils.Hpgl` |
| [#40](https://github.com/TGoodhew/GPIBUtils-NG/issues/40) | HP 7090A / 7475A / 7550A plotter (HPGL streamer prototype) | [HPGLTest](https://github.com/TGoodhew/HPGLTest) | application | `GpibUtils.Instruments.Plotters (shared HPGL send) + GpibUtils.Hpgl` |
| [#42](https://github.com/TGoodhew/GPIBUtils-NG/issues/42) | HP-GL / PCL screen-capture rendering (Hpgl.Rendering) | [GPIB-MCP](https://github.com/TGoodhew/GPIB-MCP) | mcp-server | `GpibUtils.Hpgl (parser/renderer/PCL) — shared with Plotters` |


### Oscilloscopes

| # | Instrument / component | Source repo | Type | Target module |
|---|---|---|---|---|
| [#27](https://github.com/TGoodhew/GPIBUtils-NG/issues/27) | Rigol DS1054Z Oscilloscope | [GPIBUtils](https://github.com/TGoodhew/GPIBUtils) | application | `GpibUtils.Instruments.Scopes (RigolDS1054Z)` |


### Calibrators & metrology

| # | Instrument / component | Source repo | Type | Target module |
|---|---|---|---|---|
| [#35](https://github.com/TGoodhew/GPIBUtils-NG/issues/35) | Fluke 5440A / 5440B DC Voltage Calibrator | [5440Controller](https://github.com/TGoodhew/5440Controller) | application | `GpibUtils.Instruments.Calibrators (Fluke5440)` |
| [#37](https://github.com/TGoodhew/GPIBUtils-NG/issues/37) | Fluke 5440 + HP 34401A verification runner (5440Verify) | [5440Controller](https://github.com/TGoodhew/5440Controller) | utility | `GpibUtils.Verification (plan-driven runner)` |


### Integration & shared infrastructure

| # | Instrument / component | Source repo | Type | Target module |
|---|---|---|---|---|
| [#41](https://github.com/TGoodhew/GPIBUtils-NG/issues/41) | GPIB-MCP server (generic multi-instrument MCP surface) | [GPIB-MCP](https://github.com/TGoodhew/GPIB-MCP) | mcp-server | `GpibUtils.Mcp (server) + shared instrument model DB` |
| [#42](https://github.com/TGoodhew/GPIBUtils-NG/issues/42) | HP-GL / PCL screen-capture rendering (Hpgl.Rendering) | [GPIB-MCP](https://github.com/TGoodhew/GPIB-MCP) | mcp-server | `GpibUtils.Hpgl` + `GpibUtils.Pcl` — shared with Plotters |
| [#43](https://github.com/TGoodhew/GPIBUtils-NG/issues/43) | SRQ / serial-poll completion engine (Srq.Completion) | [GPIB-MCP](https://github.com/TGoodhew/GPIB-MCP) | mcp-server | `GpibUtils.Visa/Srq` (shared by all drivers) |

<sub>The plotter items #38/#39/#40 are listed once, under **Plotters (HP-GL)** above.</sub>


### Other

| # | Instrument / component | Source repo | Type | Target module |
|---|---|---|---|---|
| [#34](https://github.com/TGoodhew/GPIBUtils-NG/issues/34) | HP 8340B Output Test (drives HP 8902A) | [GPIBUtils-Old](https://github.com/TGoodhew/GPIBUtils-Old) _(legacy)_ | application | `GpibUtils.Instruments (HP8340B source + HP8902A meter)` |


## Considered but excluded (not GPIB device-manipulation)

These were reviewed at the code level and deliberately left out of the backlog — documented here so nothing was silently dropped:

| Item | Repo | Reason |
|---|---|---|
| `SALink` | SALink | Empty WinForms stub — no instrument code exists yet |
| `HPBasic` | HPBasic | README + LICENSE only; no source on any branch |
| HP 11793A Microwave Converter | HP-Attenuator | Passive converter, no bus interface — only LO/IF planning in software |
| HP 435B Power Meter | HP435B-Test | Analog/manual DUT — operator sets switches; not bus-controlled |
| HP 11683A Range Calibrator | HP435B-Test | Manual reference source — no programmable interface |
| `HP3458AHFLKeys` | GPIBUtils-Old | Empty `Main()` stub — no VISA references |
| MB1040 serial demo | GPIBUtils-Old | RS-232 / `System.IO.Ports` rangefinder — not GPIB/VISA |
| `ToEngineeringFormat` rework | GPIBUtils-Old | Pure number-formatting helper (folded into #1 as shared code) |

Forked repos (`Multimeter_Controller`, `gpib_playground`, `FreeCal`) were out of scope by request.

## License

Released under the [MIT License](LICENSE). Copyright (c) 2026 Tony Goodhew.

---

<sub>Backlog generated from a code-level survey of the source repositories. See the pinned tracking issue for the epic view.</sub>
