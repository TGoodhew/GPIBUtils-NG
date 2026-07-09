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
