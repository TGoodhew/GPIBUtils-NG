# GPIBUtils-NG — Shared Project Memory

> Handoff/context for anyone — human or AI assistant — working on this repo. It mirrors the state of
> the consolidation effort so you can resume without prior conversation. Keep it updated as work lands.

## What this project is

A ground-up consolidation of ~18 individual GPIB/VISA instrument-control repositories (scattered across
[@TGoodhew](https://github.com/TGoodhew)'s account) into **one clean, testable .NET solution**. Each
existing instrument driver / app / utility that was found is tracked as its own **GitHub issue** in this
repo (one per instrument/driver). See the pinned **epic #44** for the full index, and `README.md` for
the target architecture.

## Locked decisions

| Decision | Value | Notes |
|---|---|---|
| Language | **C#** | Primary language enforced via `.gitattributes` (bulk data vendored). |
| Target framework | **.NET Framework 4.7.2 (`net472`)** | All projects, including WPF. Matches the legacy source; minimizes VISA.NET friction. |
| VISA layer | Pluggable **provider model**; NI parts use the **official NI assemblies** | `NationalInstruments.Visa` + `Ivi.Visa` referenced by `HintPath` from the local NI-VISA install — **not** vendored, **not** the Kelary community NuGet (dropped), **not** the official NI NuGet (net6.0-only, unusable on net472). |
| UI split | **Console = Spectre.Console**, **Windows = WPF** | Core/drivers carry no UI dependency. WinForms sources migrate to WPF. |
| Reference architecture | The **`HP-Attenuator`** repo | Core lib + Ivi.Visa + simulator + CI — the pattern the rest converges on. |

## Solution layout & status

| Project | Purpose | Status |
|---|---|---|
| `src/GpibUtils.Visa` | Vendor-neutral core: `IGpibProvider` / `IInstrumentSession`, capabilities, `GpibProviders` registry, extension stubs (Keysight/Prologix/AR488), in-memory simulator. **No vendor dependency.** | ✅ done |
| `src/GpibUtils.Visa.Ni` | NI-VISA (default) + native NI-488.2 providers on the official NI assemblies (HintPath). Auto-registered by reflection when deployed. | ✅ done |
| `src/GpibUtils.Common` | Shared helpers — `ToEngineeringFormat` (consolidated + hardened). | ✅ done |
| `src/GpibUtils.Console` | Runnable Spectre.Console.Cli app `gpibutils` (`providers`/`discover`/`query`/`idn` + `config address` + 17 device branches). | ✅ done (base + config + 17 devices) |
| `tests/*` (Visa, Common, Switches, SignalSources, Meters, Counters, PowerSupplies, Scopes, Wpf) | xUnit. | ✅ 286 tests green |
| `src/GpibUtils.Instruments.Scopes` | Oscilloscopes (`IOscilloscope`). **Rigol DS1054Z** (#27). | 🟡 done, awaiting HW verification |
| `src/GpibUtils.Instruments.Calibrators` | DC voltage calibrators (`IDcVoltageCalibrator`). **Fluke 5440A/5440B** (#35). | 🟡 done, awaiting HW verification |
| `src/GpibUtils.Instruments.Analyzers` | Spectrum/signal analyzers. **HP 8560E** (#13, `ISpectrumAnalyzer`, #43 SRQ-edge), **Agilent E4406A VSA** (#12), **HP 85620A** mass-memory via 8563E (#10/#14). | 🟡 done, awaiting HW verification |
| `src/GpibUtils.Instruments.Plotters` | HP-GL pen plotters (`IPlotter`). **HP 7090A/7475A/7550A** (#38/#39/#40, one canonical `HpPlotter`); previews via #42. | 🟡 done, awaiting HW verification |
| `src/GpibUtils.Measurement` | Attenuation-vs-frequency orchestration — `MeasurementEngine` + 11793A `MicrowaveConverter` LO/IF planner (#34), drives 8340B+8673B+11713A+8902A. | 🟡 done, awaiting HW verification |
| `src/GpibUtils.Verification` | Cross-instrument verification — `Fluke5440Verifier` (#37): 5440 → 34401A ppm/PASS-FAIL + CSV. | 🟡 done, awaiting HW verification |
| `src/GpibUtils.Instruments.Switches` | Switch/attenuator drivers. **HP 11713A** (#6) + **HP 3499A** (#4). | 🟡 done, awaiting HW verification |
| `src/GpibUtils.Instruments.Counters` | Counters. **HP 53131A** (#21/#5, universal, #43 SRQ) + **HP 5351A** (#20) + **HP 5342A** (#32) microwave. | 🟡 done, awaiting HW verification |
| `src/GpibUtils.Instruments.SignalSources` | Signal sources. **HP 8340B** (#7), **8673B** (#8), **8350B** (#22), **3325B** synth (#28/#29), **Keysight E4438C ESG** (#11, ARB). | 🟡 done, awaiting HW verification |
| `src/GpibUtils.Instruments.Meters` | Receivers / power meters / DMMs. **8902A** (#9), **34401A** (#36/#17), **E4418B** (#25, `IPowerMeter`, #43 SRQ), **438A** (#33), **DM3058** (#26), **3458A** (#31). | 🟡 done, awaiting HW verification |
| `src/GpibUtils.Instruments.PowerSupplies` | DC power supplies (`IDcPowerSupply`). **HP E3633A** (#19) + **Rigol DP832** (#15, 3-ch). | 🟡 done, awaiting HW verification |
| `src/GpibUtils.Instruments.*` (Scopes, Analyzers, Calibrators, plotters) | Remaining categories. | ⬜ in progress (this session) |
| `src/GpibUtils.Wpf` | WPF/MVVM desktop shell (providers/discover/query on the core). | ✅ done (needs a visual smoke test) |
| `src/GpibUtils.Visa/Srq` | Shared SRQ/serial-poll completion engine (`CompletionWaiter` + data-driven `StatusModel`, `IStatusChannel`, `SessionStatusChannel`). **#43 ported.** | ✅ done |
| `src/GpibUtils.Hpgl` | HP-GL/2 parser + renderer (`HpglRenderer.RenderToPng`/`RenderToSvg`). **#42 filled** (ported from GPIB-MCP; KE5FX-derived). | ✅ done |
| `src/GpibUtils.Mcp` | MCP JSON-RPC/stdio server + 55-model instrument DB, over the provider model; `srq_wait` (→#43), `screen_capture` (→#42). **#41 filled.** | ✅ done |
| CI | GitHub Actions: build + test whole solution, no NI. | ✅ done |

## Development workflow (no-hardware build policy)

Development happens **without the physical instruments**, so building and hardware verification are
decoupled. Full board: [`docs/HARDWARE_VERIFICATION.md`](docs/HARDWARE_VERIFICATION.md) (mirrored by pinned
HW-verification tracking issue **#46**); changes logged in [`CHANGE_LOG.md`](CHANGE_LOG.md).

- **`main`** = always-green integration line (builds + passes **Simulated**-provider tests). Everything stacks on it.
- Per issue: branch **`feat/<issue#>-<slug>`** → port driver + simulator + tests + CLI branch (#45).
- **Merge to `main` on simulator/unit-test green — HW verification is _not_ a merge gate** (lets the next driver build immediately).
- **Do not close the issue on merge.** Label it **`verification-needed`**, add the bench checklist, register it on the board, and tag the merge commit **`verify/<issue#>-<instrument>`**.
- At the bench: run the checklist against real HW, record pass/fail + FW/serial/date, **then** close (or open a follow-up on a discrepancy).

## Build & test

```
# .NET 10 SDK builds net472 via Microsoft.NETFramework.ReferenceAssemblies (no full targeting pack needed)
dotnet build GPIBUtils-NG.sln
dotnet test  GPIBUtils-NG.sln

# Run the console (Simulated provider needs no hardware)
gpibutils providers
gpibutils idn   GPIB0::5::INSTR  --provider Simulated
gpibutils query GPIB0::14::INSTR "MEAS:VOLT?" --provider Simulated --engineering V
```

`GpibUtils.Visa.Ni` uses the real NI providers where NI-VISA is installed, and **builds an "NI-VISA
unavailable" stub otherwise** — so the whole solution (and CI) builds with zero NI setup; no need to
remove any project reference. Pass `-p:RequireNi=true` to hard-fail when NI is expected. NI DLLs are
**never committed** (they resolve from the install into `bin/`).

## Key conventions (apply to all future work)

- **Provider model** — drivers only see `IInstrumentSession`; the wire is an `IGpibProvider` selected
  via `GpibProviders` (default `NI-VISA`, override in code or `GPIBUTILS_GPIB_PROVIDER`). Add a vendor
  by creating `GpibUtils.Visa.<Vendor>` and registering it (or adding to the reflection-load list). Full
  guide: [`docs/implementing-a-gpib-provider.md`](docs/implementing-a-gpib-provider.md).
- **CLI-first (issue #45)** — every instrument must be fully operable from the command line via a
  hierarchical Spectre.Console.Cli tree (`gpibutils <device> <action>`) with self-documenting `--help`
  at **every** level; no interactive-only paths. Each migrated driver adds its own command branch plus
  the shared global options `--provider` / `--address` / `--timeout`.
- **Porting a driver** — no hardcoded GPIB addresses (make the resource configurable); move onto the
  shared transport; add a simulator/mock so it builds & tests without hardware; port/author tests;
  reconcile duplicate implementations into one canonical driver (see the "Related implementations" note
  in each issue).
- **Don't re-copy** `ToEngineeringFormat` — use `GpibUtils.Common`.
- **Bench uses HP-IB bus extenders (HP 37204A or similar)** — this has two hard consequences:
  1. **Bus-scan discovery is untrustworthy.** An extender ACKs the address handshake for its whole remote
     segment, so a VISA scan (`Rm.Find`) reports *every* GPIB address 0–30 as present — all phantom. Never
     use `discover` / the WPF Discover button to enumerate real instruments; drive by explicit resource
     string (`--address` / per-driver `DefaultResource`). Diagnostic: **seeing (nearly) every address in use
     means an extender is in the path** — both front-ends warn when `Discover` returns ≥15 resources.
  2. **SRQ / serial-poll must tolerate extender latency.** Keep timeouts generous and make the #43
     `Srq.Completion` engine forgiving of the longer, variable turnaround across the link. Directly affects
     the HP 8902A (SRQ-based measurement-complete handshake).

## Migration backlog map

- **#1** foundation · **#2–#43** one issue per instrument/driver found (42 items) · **#44** pinned epic ·
  **#45** the CLI hierarchical-help requirement.
- **Consolidate duplicates** (same instrument, multiple source repos): HP 8673B → #3/#8/#18/#23 (canonical
  = #8); HP 8902A → #2/#9/#24/#34 (canonical = #9, richest); HP 53131A → #5/#21 (canonical = #21, ✅ done);
  HP 34401A → #17/#36 (canonical = #36, ✅ done — driver only; the 5440-verify harness & HP435B PDF app are
  deferred follow-ups);
  Rigol DM3058 → #26 supersedes #30; HP 3325B → #28/#29; HP 8340A/B → #7/#16/#34; HP-GL plotters →
  #38/#39/#40 share #42's renderer; 85620A → #10/#14; SRQ handling → #43 replaces ad-hoc code. Details in #44.

## Current status / resume point

- **Foundation (#1) essentially complete:** core transport + Common + Console + **WPF shell** + **CI** all
  landed; `Hpgl`/`Mcp` scaffolded (filled by #42, #41); `Visa.Ni` degrades gracefully without NI so the
  whole solution builds with zero NI setup. **WPF visual smoke test passed** (2026-07-09) — #1 ready to close.
- **SRQ completion engine landed** (#43, 2026-07-10): `GpibUtils.Visa.Srq` — the shared, data-driven
  `CompletionWaiter` (SRQ-edge + direct-bit flows) driven by a `StatusModel`, decoupled via `IStatusChannel`
  with `SessionStatusChannel` bridging a live session. Headless-tested against a virtual-clock 8560 simulator.
  Defaults kept generous for HP-IB extender latency. It targets **SRQ-enable-mask-driven** completions
  (8560-style sweeps); instruments with their own settled-read handshake (the 8902A) keep theirs.
  **First real consumer = HP 53131A (#21, 2026-07-15):** its `*ESE 1`/`*SRE 32`/`INIT;*OPC` completion runs
  through `CompletionWaiter` (direct-bit flow) via `SessionStatusChannel`, with the `StatusModel` built in
  `Hp53131A.StatusModel()` — proof the engine drives a driver end to end, headlessly.
- **HP 53131A Universal Counter landed** (#21/#5, 2026-07-15): new `GpibUtils.Instruments.Counters` project
  (`IFrequencyCounter`); the **canonical** 53131A, deduping the two identical `GPIBUtils/HPDevices` copies
  (#21) and the SCPI reader in `HP3499Demo` (#5). Frequency on ch 1–3 (`CONF:FREQ (@n)`), 50 Ω/1 MΩ input
  impedance. **First driver to consume the #43 SRQ engine** — completion via `CompletionWaiter` +
  `SessionStatusChannel`, `StatusModel` in `Hp53131A.StatusModel()`; timeout → typed `Hp53131AException`.
  `Hp53131ASimulatedDevice` + 18 tests; `gpibutils hp53131a idn|init|reset|freq`. Default `GPIB0::3::INSTR`
  (factory default per Programming Guide; **demo used bench ::23::** — confirm the real bench address).
- **HP 3499A Switch/Control System landed** (#4, 2026-07-15): plain-SCPI mainframe driver in
  `GpibUtils.Instruments.Switches`. Relay open/close/state on the `snn` (slot+2-digit channel) scheme
  (`ROUT:CLOS`/`OPEN`/`CLOS? (@snn)`) + card inventory (`SYST:CTYPE?`). 44472A/44476B are plug-ins on the
  mainframe scheme, not separate instruments; N2236A digital-IO not driven (source didn't either).
  `Hp3499ASimulatedDevice` + 17 tests; `gpibutils hp3499a idn|init|cards|close|open|state`. Default
  `GPIB0::9::INSTR` (factory default per User's & Programming Guide, matches source).
- **HP 34401A Digital Multimeter landed** (#36/#17, 2026-07-15): the **canonical** 34401A DMM in
  `GpibUtils.Instruments.Meters` — a plain SCPI `IDigitalMultimeter` (new interface). Consolidates the two
  source apps: the rich SCPI menu from `5440Controller/34401AController` (canonical) + the buffered
  recorder-output acquisition from `HP435B-Test`. Full surface: CONFigure (all functions + range/res),
  SENSe (NPLC / autorange / input-Z / autozero), TRIGger + SAMPle, CALCulate math (null/dB/dBm/limits/avg
  stats), DISPlay; single + burst `READ?`/`FETCh?` with `DmmStatistics` (min/max/avg/sample-σ, Welford).
  `Hp34401ASimulatedDevice` + 32 tests; `gpibutils hp34401a idn|init|reset|read|measure|stats|selftest|
  errors|display`. Default address `GPIB0::22::INSTR` (34401A factory default, confirmed against the User's
  Guide p.91). Reads use plain blocking `READ?` (the 34401A returns the whole burst in one response) rather
  than the source's *OPC/SRQ handshake — no #43 engine needed for a bounded burst. 🟡 awaiting HW.
  **Deferred follow-ups (own issues, not this driver):** the 5440A-calibrator ppm-verification harness
  (needs a 5440 driver + dual-session orchestration) and the HP 435B power-meter PDF test app (Syncfusion).
- **HP 8902A Measuring Receiver landed** (#9, 2026-07-10): seeds `GpibUtils.Instruments.Meters`
  (`IMeasuringReceiver`) — the canonical 8902A. Tuned RF Level (dB) / RF power (dBm) / frequency, cal-factor
  tables, zero+sensor-cal, Track Mode, Avg/Sync detectors; settled-read Data-Ready serial-poll completion
  (hardware-verified inline — deliberately NOT rewired onto #43's engine before bench re-verification).
  `Hp8902ASimulatedDevice` + 21 tests; `gpibutils hp8902a init|preset|status|frequency|power|level`.
  Default address `GPIB0::14::INSTR` (8902A factory-default HP-IB address, confirmed). 🟡 awaiting HW.
- **Per-instrument address config landed** (#54, 2026-07-10): `InstrumentAddressStore` in `GpibUtils.Common`
  (JSON at `%APPDATA%\GpibUtils\addresses.json` or `$GPIBUTILS_CONFIG`) persists the bench's real addresses;
  resolution precedence is **`--address` > configured > `DefaultResource`**. Console `gpibutils config address
  list|get|set|clear` + `config path`. Every driver's `DefaultResource` verified against its manual (2026-07,
  see [manuals folder]) — 11713A=28, 8673B=19, 8902A=14 match; **8340B=20 documented as a bench remap** off
  the manual default 19 (shares 8673B's factory 19). WPF surfacing deferred until it has per-instrument panels.
- **Extender-aware discovery caveat landed** (2026-07-09): `gpibutils discover` and WPF Discover now warn
  when a scan returns ≥15 resources that an HP-IB bus extender is in the path and the list is phantom
  (see Key conventions).
- **Drivers landed** (all 🟡 awaiting HW, board / issue #46): HP 11713A (#6), HP 8340B (#7), HP 8673B (#8),
  HP 8902A (#9), HP 34401A (#36/#17), HP 53131A (#21/#5), HP 3499A (#4); tags `verify/6-hp11713a` /
  `verify/7-hp8340b` / `verify/8-hp8673b` / `verify/9-hp8902a` / `verify/36-hp34401a` / `verify/21-hp53131a` /
  `verify/4-hp3499a`. **187 tests green.**
- **As of 2026-07-15:** `main` green (**371 tests**), **no open PRs**. Earlier batch migration (PRs #57–#68):
  34401A (#57), 53131A+3499A (#58), E3633A (#59), DP832 (#60), E4418B (#61), 438A (#62), DM3058 (#63),
  3458A (#64), 5351A (#65), 5342A (#66), 8350B (#67), 3325B (#68); DS1054Z (#69). Then the **final driver
  batch (PRs #71–#75):** Fluke 5440A (#71), E4438C (#72), 8560E (#73), E4406A (#74), 85620A (#75) — all
  merged, each `verification-needed` + bench checklist (not closed). **Every legacy-repo instrument driver is
  now ported.** **Closed as consolidated/superseded:** #30 (→#26), #29 (→#28), #16 (→#7), #18/#23/#3 (→#8),
  #24/#2 (→#9). All legacy source repos cloned locally under `C:\Users\Tony\Source\Repos`.
- **DS1054Z landed** (#27, PR #69): new `GpibUtils.Instruments.Scopes` (`IOscilloscope`) — Rigol DS1054Z
  (run/stop/single/autoscale, channel display, `:MEASure:ITEM?`). USB/LXI (no GPIB).
- **Driver-migration backlog CLEARED** (2026-07-15, PRs #71–#75 — all 🟡 awaiting HW, `verification-needed`):
  - **#35 Fluke 5440A/5440B calibrator** (PR #71, tag `verify/35-fluke5440a`): new `Calibrators` proj,
    `IDcVoltageCalibrator`. Mnemonic (no *IDN?; `GVRS`=firmware). SOUT/GOUT/INCR/SREF/GREF, OPER/STBY,
    ESNS/ISNS, EGRD/IGRD, DIVY/DIVN, BSTV/BSTC/BSTO, SVLM/SCLM limits, GSTS/GERR/GONG, SSRQ/GSRQ/GSPB,
    TSTA/TSTD/TSTH. G7 numeric format. Addr **7**. 20 tests. **PR #71 also fixed a pre-existing CLI-wide
    bug:** `hp3325b set` aliased amplitude to `-a`, colliding with the global `-a|--address` → Spectre model
    validation failed for the ENTIRE command tree (every command errored "Option -a is duplicated"). The CLI
    was unrunnable on `main` before this. Amplitude is now `-l|--amplitude`.
  - **#11 Keysight E4438C ESG** (PR #72, tag `verify/11-e4438c`): `SignalSources`, `ISignalSource` + Hz/ARB
    surface. `:FREQ:FIXed`/`:POW:LEVel` (+MIN/MAX), RF/mod on-off, reference auto/source; Dual ARB — download
    interleaved 16-bit two's-complement **big-endian** I/Q to WFM1 as an IEEE-488.2 definite-length block,
    select/play, copy to/from NVWFM. Completion via `*OPC?` + `:SYST:ERR?` (not SRQ). Addr **19** (lab). 20 tests.
  - **#13 HP 8560E spectrum analyzer** (PR #73, tag `verify/13-hp8560e`): new `Analyzers` proj,
    `ISpectrumAnalyzer`. **First #43 SRQ-EDGE consumer** — `SingleSweep` arms `RQS 16` + `SNGLS;TS;`,
    `CompletionWaiter` waits for busy then request-service (0x40), via `SessionStatusChannel`; StatusModel
    (Table 7-9, RequestServiceBit set) built in `Hp8560E.StatusModel()`. IP/CF/SP/RB/VB/ST, TRA?, MKPK HI/
    MKF?/MKA?. Addr **18**. 15 tests (incl. complete/error/timeout). **Added a reusable `OnSerialPoll` hook to
    the core `SimulatedInstrument`** so an SRQ-edge sweep's busy→done transition can be advanced per poll
    headlessly (the 53131A's direct-bit flow needed no such hook).
  - **#12 Agilent E4406A VSA** (PR #74, tag `verify/12-e4406a`): `Analyzers`. Basic single-measurement mode;
    four SCPI verbs (`:MEASure`/`:CONFigure`/`:READ`/`:FETCh` + result index) → parsed scalar sets; typed
    ChannelPower (span+integ-BW → [power,PSD]) and ACP (no settable span, FW-truthed). No global span,
    blocking `:READ` (no SRQ). Addr **18** (manual; app used 17). 15 tests.
  - **#10/#14 HP 85620A mass memory / 8563E** (PR #75, tag `verify/10-hp85620a`): `Analyzers`. Driven through
    the analyzer: `ID?`, `MSDEV MEM|CARD`, `CATALOG?;` (parsed entries + BYTES FREE), `CARDSTORE`/`CARDLOAD
    %name%;`, `DISPOSE ALL;`, `FUNCDEF`. Completion via `DONE?;`/`ERR?;` (NOT SRQ) → typed `Hp85620AException`.
    Addr **18**. 15 tests. **Deferred (own follow-up on #14):** the raw SRAM-image decode (bit de-scramble +
    DLP extraction from a binary dump, `DLPBits/Program.cs`) and card FORMAT (not possible over HP-IB) —
    offline/file utilities, not live-instrument functions.
- **New tracking issue #70** (2026-07-15): review the ~424 PDFs in the Manuals folder and migrate a driver for
  every VISA/488.2-controllable device not yet backlogged (triage table + per-instrument issues). This is the
  next discovery source now that the legacy-repo backlog's drivers are all ported.
- **Both scaffolds FILLED (2026-07-15, PRs #76–#77) + #43 closed:**
  - **#42 HP-GL/2 rendering landed** (PR #76, tag `verify/42-hpgl`): ported GPIB-MCP's `Hpgl.Rendering` into
    `GpibUtils.Hpgl` — `HpglRenderer.RenderToPng` (System.Drawing) / `RenderToSvg`, `HpglParser`, single-stroke
    `StrokeFont`. KE5FX `7470.cpp`-derived (attribution preserved). **51 tests** incl. a pixel render-regression
    vs a golden 8563E capture. CLI `gpibutils hpgl render <file.plt> [-o out] [--svg]`, visually verified.
    **Unblocks plotters #38/#39/#40.**
  - **#41 GPIB-MCP server landed** (PR #77, tag `verify/41-mcp`): JSON-RPC 2.0 stdio MCP server + the shared
    **55-model instrument DB**, rebuilt on the **provider model** (not the source's NI-VISA manager). Tools:
    `list_providers`/`discover`/`query`/`write`/`read`/`clear`/`identify`/`db_list`/`db_get`/`db_match`/
    `srq_wait`/`screen_capture`. Cross-wiring: **`srq_wait`** runs a DB `statusModel` through the #43
    `CompletionWaiter` (a JSON `statusModel` block deserializes straight into `GpibUtils.Visa.Srq.StatusModel`;
    the 8563E def carries one); **`screen_capture`** renders a model's HP-GL capture profile via #42. CLI
    `gpibutils mcp serve|tools`; user DB at `%LOCALAPPDATA%\GpibUtils\Mcp`. **14 tests**; live stdio handshake
    verified. Uses Newtonsoft.Json (first in the solution).
  - **#43 CLOSED** (2026-07-15): the SRQ engine is complete and now wired into the MCP model DB (its last open
    checklist item). Pure-software infrastructure, no bench gate.
  - **Plotters + both apps landed; #45/#54 closed (2026-07-15, PRs #78–#80):**
    - **#38/#39/#40 plotters** (PR #78, `verify/38-plotters`): one canonical `HpPlotter` (7090A/7475A/7550A) in
      `GpibUtils.Instruments.Plotters`; streams HP-GL, previews to PNG via #42. 16 tests. CLI `plotter
      idn|init|plot|window` with `-m <model>`/`--preview`.
    - **#34 attenuation-measurement app** (PR #79, `verify/34-measurement`): `GpibUtils.Measurement` — ported
      `MeasurementEngine` + 11793A `MicrowaveConverter` planner; orchestrates 8340B→8673B(LO)→11713A→8902A.
      Added `IStepAttenuator` (Switches, Hp11713A implements). 8 tests vs an ideal FakeBench. CLI `measure
      sweep`. **The biggest milestone — the app side of #6.**
    - **#37 5440 verification runner** (PR #80, `verify/37-verify`): `GpibUtils.Verification` —
      `Fluke5440Verifier` drives the 5440 (#35) through a plan, reads back on the 34401A (#36), ppm + PASS/FAIL
      + CSV. 11 tests vs linked simulators. CLI `verify 5440` (exit 1 on FAIL).
    - **#45 (CLI-first) and #54 (address config) CLOSED** — software-complete standards, no hardware gate:
      every instrument has a self-documenting CLI branch; `InstrumentAddressStore` + `config address …` persist
      bench addresses (24 instruments). #54's only deferred piece is WPF surfacing (WPF-panels track).
  - **Small software follow-ups done (2026-07-15, PR #81 — pure software, no bench gate):**
    - **85620A SRAM-image decode** (#14): `Hp85620ASramImage` — offline de-scramble (address + data bit
      permutations) + DLP extraction between the 0x10,0x80 / 0x3B,0xFF markers; CLI `hp85620a decode`. The
      extracted DLP bodies feed the existing `FUNCDEF` path. This is #14's remaining non-hardware scope.
    - **E4406A typed CCDF** (#12): `MeasureCcdf` → `CcdfResult` (PSTatistic [0]/[1]/[8] = avg/prob/PAPR).
  - **Follow-ups NOT done (with reasons):**
    - **8340A/8340B output-test harness** — subsumed by #34's `MeasurementEngine` (`DetectSignal` /
      `MeasureRfPower`); no separate WPF harness planned.
    - **HP435B PDF report** — the `HP435B-Test` source repo is **not cloned locally** and it used **Syncfusion**
      (a heavy commercial PDF dependency we don't want). Blocked/skipped until the source is available and a
      non-Syncfusion report format is chosen.
    - **Full E4406A typed-result layer** (ACP/Waveform/Spectrum) — needs the multi-model scalar-layout dialect
      abstraction (kept upstream); larger than a small follow-up, deferred.
  - **Remaining work:** discovery issue **#70** (triage the ~424-PDF Manuals folder) and future **WPF
    instrument panels**. **All source repos cloned locally** under `C:\Users\Tony\Source\Repos` (except
    `HP435B-Test`).

- **Next step — pick a track (recommendation = ①):**
  1. **Build the end-to-end attenuation-measurement app** *(recommended)* — all four `HP-Attenuator`
     instruments (11713A/8340B/8673B/8902A) are migrated, so the deferred `MeasurementEngine`
     (orchestrates source→LO→attenuator→receiver) is unblocked. Port it from
     `C:\Users\Tony\Source\Repos\HP-Attenuator\src\HP-Attenuator.Core\Measurement\MeasurementEngine.cs`.
     Maps to issue #34 / the app side of #6. Biggest milestone; first real bench demo.
  2. **Fill a scaffold:** **#41 GPIB-MCP server** (brings the instrument DB → also the `StatusModel`
     source for the #43 SRQ engine + a control surface) or **#42 HP-GL rendering** (unblocks plotters
     #38/#39/#40).
  3. **Work #70** — triage the Manuals folder and migrate any newly-discovered programmable instruments.
  4. **Plotters #38/#39/#40** — now unblocked by #42's renderer (drive the plotter over the bus + render).
  5. **WPF instrument panels** + finish #54's WPF address-config surfacing.
  - **Quick backlog cleanup (any time):** close the consolidated duplicate issues as superseded by the
    canonical migrations — 8673B → #3/#18/#23, 8902A → #2/#24, 8340B → #16. Not yet done.
  - **Blocked:** bench verification of the whole `verification-needed` set until back in Renton (board #46).

- **Test count: 484 green** (2026-07-15), 16 test assemblies, `main` clean, no open PRs. New projects since the
  driver batch: `GpibUtils.Hpgl` (#42, +51 tests), `GpibUtils.Mcp` (#41, +14 tests), `Plotters` (#38-40, +16), `Measurement` (#34, +8), `Verification` (#37, +11). The `mcp serve` command is
  the LLM integration surface; `hpgl render` renders captured plots.

> Cross-machine note: this file (in-repo) is the durable handoff and travels via git. The assistant's
> local file-memory (`~/.claude/projects/.../memory/`) is machine-local and does NOT follow you — but it
> only mirrors what's here plus the reference: **manuals at `C:\Users\Tony\OneDrive\Documents\Manuals`**
> are the authority for default GPIB addresses; **whenever a `DefaultResource` is added, verify it against
> the manual and document any divergence in code** (the rule that produced the 8340B=20-vs-19 note).

---
_This file is the human/tool-readable mirror of the assistant's working notes. If you use GitHub Copilot
in VS Code, you can also point it here via `.github/copilot-instructions.md`._
