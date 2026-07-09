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
| `src/GpibUtils.Console` | Runnable Spectre.Console.Cli app `gpibutils` (`providers`/`discover`/`query`/`idn` + `hp11713a` branch). | ✅ done (base + first device) |
| `tests/*` (Visa, Common, Instruments.Switches) | xUnit. | ✅ 46 tests green |
| `src/GpibUtils.Instruments.Switches` | Switch/attenuator drivers. **HP 11713A** (#6) ported. | 🟡 done, awaiting HW verification |
| `src/GpibUtils.Instruments.*` (other categories) | Instrument drivers by category. | ⬜ not started |
| `src/GpibUtils.Hpgl` | HP-GL / PCL parser + renderer. | ⬜ not started |
| `src/GpibUtils.Mcp` | MCP server surface + instrument DB. | ⬜ not started |
| `src/GpibUtils.Wpf` | WPF/MVVM desktop shell. | ⬜ not started |
| CI | Build in simulation, no hardware. | ⬜ not started |

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

`GpibUtils.Visa.Ni` builds only where NI-VISA is installed (it errors with guidance otherwise); a
non-NI contributor removes that one project reference. NI DLLs are **never committed** (they resolve
from the install into `bin/`).

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

## Migration backlog map

- **#1** foundation · **#2–#43** one issue per instrument/driver found (42 items) · **#44** pinned epic ·
  **#45** the CLI hierarchical-help requirement.
- **Consolidate duplicates** (same instrument, multiple source repos): HP 8673B → #3/#8/#18/#23 (canonical
  = #8); HP 8902A → #2/#9/#24/#34 (canonical = #9, richest); HP 53131A → #5/#21; HP 34401A → #17/#36;
  Rigol DM3058 → #26 supersedes #30; HP 3325B → #28/#29; HP 8340A/B → #7/#16/#34; HP-GL plotters →
  #38/#39/#40 share #42's renderer; 85620A → #10/#14; SRQ handling → #43 replaces ad-hoc code. Details in #44.

## Current status / resume point

- **HP 11713A (#6) landed** on `main` — first driver + `gpibutils hp11713a` CLI branch, 46 tests green,
  tagged `verify/6-hp11713a`. Issue #6 is open in **🟡 Verification Needed** state (see the board /
  issue #46); run the bench checklist when hardware is available.
- **Next step:** migrate the next driver (e.g. HP 3499A switch #4, or an HP-Attenuator source driver),
  reusing the #6 pattern (branch `feat/<issue#>-<slug>`, simulator + tests, CLI branch, then
  verification-needed). After more drivers: `GpibUtils.Hpgl`, `GpibUtils.Mcp`, `GpibUtils.Wpf`, and CI.

---
_This file is the human/tool-readable mirror of the assistant's working notes. If you use GitHub Copilot
in VS Code, you can also point it here via `.github/copilot-instructions.md`._
