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
| `src/GpibUtils.Console` | Runnable Spectre.Console.Cli app `gpibutils` (`providers`/`discover`/`query`/`idn` + `config address` + `hp11713a`/`hp8340b`/`hp8673b`/`hp8902a` branches). | ✅ done (base + config + 4 devices) |
| `tests/*` (Visa, Common, Instruments.Switches, Instruments.SignalSources, Meters, Wpf) | xUnit. | ✅ 120 tests green |
| `src/GpibUtils.Instruments.Switches` | Switch/attenuator drivers. **HP 11713A** (#6) ported. | 🟡 done, awaiting HW verification |
| `src/GpibUtils.Instruments.SignalSources` | Signal sources (`ISignalSource`/`ILocalOscillator`). **HP 8340B** (#7), **HP 8673B** (#8) ported. | 🟡 done, awaiting HW verification |
| `src/GpibUtils.Instruments.Meters` | Measuring receivers / power meters (`IMeasuringReceiver`). **HP 8902A** (#9, canonical) ported. | 🟡 done, awaiting HW verification |
| `src/GpibUtils.Instruments.*` (other categories) | Instrument drivers by category. | ⬜ not started |
| `src/GpibUtils.Wpf` | WPF/MVVM desktop shell (providers/discover/query on the core). | ✅ done (needs a visual smoke test) |
| `src/GpibUtils.Visa/Srq` | Shared SRQ/serial-poll completion engine (`CompletionWaiter` + data-driven `StatusModel`, `IStatusChannel`, `SessionStatusChannel`). **#43 ported.** | ✅ done |
| `src/GpibUtils.Hpgl` | HP-GL / PCL parser + renderer. | 🏗 scaffold (filled by #42) |
| `src/GpibUtils.Mcp` | MCP server surface + instrument DB. | 🏗 scaffold (filled by #41) |
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
  = #8); HP 8902A → #2/#9/#24/#34 (canonical = #9, richest); HP 53131A → #5/#21; HP 34401A → #17/#36;
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
  (8560-style sweeps); instruments with their own settled-read handshake (the 8902A) keep theirs. CLI/MCP
  exposure deferred until the first mask-driven consumer.
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
  HP 8902A (#9); tags `verify/6-hp11713a` / `verify/7-hp8340b` / `verify/8-hp8673b` / `verify/9-hp8902a`.
  **120 tests green.**
- **All PRs merged as of 2026-07-10** — no open PRs. `main` is green (120 tests). Merged this session:
  #52 (SRQ #43), #53 (8902A #9), #55 (address docs), #56 (address config #54); plus the `.gitignore`
  entry for `.claude/settings.local.json`.

- **Next step — pick a track (recommendation = ①):**
  1. **Build the end-to-end attenuation-measurement app** *(recommended)* — all four `HP-Attenuator`
     instruments (11713A/8340B/8673B/8902A) are now migrated, so the deferred `MeasurementEngine`
     (orchestrates source→LO→attenuator→receiver) is unblocked. Port it from
     `C:\Users\Tony\Source\Repos\HP-Attenuator\src\HP-Attenuator.Core\Measurement\MeasurementEngine.cs`.
     Maps to issue #34 / the app side of #6. Biggest milestone; first real bench demo.
  2. **Next driver (breadth):** lowest-risk = **HP 34401A DMM (#17/#36)** (plain SCPI); or HP 3499A
     switch (#4), HP 53131A counter (#5/#21). `Meters` category also awaits power meters #25/#33.
  3. **Fill a scaffold:** **#41 GPIB-MCP server** (brings the instrument DB → also the `StatusModel`
     source for the #43 SRQ engine + a control surface) or **#42 HP-GL rendering** (unblocks plotters
     #38/#39/#40).
  4. **WPF instrument panels** + finish #54's WPF address-config surfacing.
  - **Quick backlog cleanup (any time):** close the consolidated duplicate issues as superseded by the
    canonical migrations — 8673B → #3/#18/#23, 8902A → #2/#24, 8340B → #16. Not yet done.
  - **Blocked:** bench verification of #6–#9 until back in Renton (board #46).

> Cross-machine note: this file (in-repo) is the durable handoff and travels via git. The assistant's
> local file-memory (`~/.claude/projects/.../memory/`) is machine-local and does NOT follow you — but it
> only mirrors what's here plus the reference: **manuals at `C:\Users\Tony\OneDrive\Documents\Manuals`**
> are the authority for default GPIB addresses; **whenever a `DefaultResource` is added, verify it against
> the manual and document any divergence in code** (the rule that produced the 8340B=20-vs-19 note).

---
_This file is the human/tool-readable mirror of the assistant's working notes. If you use GitHub Copilot
in VS Code, you can also point it here via `.github/copilot-instructions.md`._
