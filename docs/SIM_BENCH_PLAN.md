# Simulated-bench coupling for the verification harness — design + handoff

**Status:** designed, fully researched, **not yet implemented**. Written 2026-07-23 so the work can be picked
up on another machine / Claude Code install with nothing lost.

**Branch:** `claude/instrument-verification-harness-z7phx1` (PR #174), local alias `verify-174`.

---

## 1. Where the overall task stands

The job is the repo's `HANDOFF.md` (lives on `origin/main` @ `45bece3`, *not* on this branch): merge two open
**draft** PRs, then surface two admin-only steps.

| PR | Branch | What it is |
|----|--------|------------|
| **#174** | `claude/instrument-verification-harness-z7phx1` | Cross-instrument verification harness. Extends the pre-existing `GpibUtils.Verification` project (the Fluke-5440 work already on main) with `References/`, `Catalog/`, `SignalSourceVerifier`, `DcSourceVerifier`. Adds Console `verify harness` (interactive) + `verify source` (one-shot). Touches no `.sln` — the project was already in it. Builds clean; **36/36 Verification.Tests pass**. |
| **#175** | `claude/ci-cloud-environment` | Hardened Windows CI (full sln, Release, trx artifacts), a **SessionStart hook** that is a genuine LOCAL NO-OP (`exit 0` unless `CLAUDE_CODE_REMOTE=true`), and `GPIBUtils-NG.NoWpf.slnf` (52 non-WPF projects). Reviewed — safe. |

Both are CI-green on `build-and-test`, MERGEABLE/CLEAN, still drafts. They do not conflict (disjoint files).

Verified during design: **net472 confirmed everywhere** (Tony's explicit requirement — the NI assemblies need
it). All 54 csproj target `net472` with no implicit targets; `Directory.Build.props` pins
`Microsoft.NETFramework.ReferenceAssemblies.net472` and sets `LangVersion=latest`; the NI binding
(`GpibUtils.Visa.Ni`) is untouched. Neither PR changes any TFM. .NET SDK 10.0.301 is installed locally and the
full-solution Windows build works.

## 2. The finding that blocked the merge

The harness docstrings and `HANDOFF.md` claim it is "**fully drivable hardware-free against the Simulated
provider**". That is **false at the CLI level**:

```
verify source --provider Simulated --dut hp8340b --power-ref e4418b --freq-ref hp53131a
```

…hangs 20–30 s per reference and then errors (`E4418B zero/cal did not complete — Timed out`).

**Cause.** `SimulatedGpibProvider.Open` auto-creates a **generic** `SimulatedInstrument` for any unregistered
address (`src/GpibUtils.Visa/Simulation/SimulatedGpibProvider.cs`, `Open`, ~line 46). The specific
`*SimulatedDevice` models (`HpE4418BSimulatedDevice`, `Hp8902ASimulatedDevice`, …) are only ever instantiated
**inside unit tests** — no front-end (Console / WPF / MCP) seeds them. A generic instrument never raises the
status bits the reference drivers' completion handshakes wait on, so the drivers correctly time out.

This is **not a #174 regression** — the whole CLI's Simulated mode is generic. The harness just surfaces it
harshly because its reference drivers (correctly) wait on SRQ/OPC completion. The pre-existing
`verify 5440 --provider Simulated` doesn't hang only because it returns garbage 0 V instead of waiting on SRQ.
Unit tests stay green because they use fakes.

**Tony's decision (2026-07-23, via AskUserQuestion): "fix sim demo first", then merge #174, then #175.**

## 3. Design

Goal: `verify harness --provider Simulated` and `verify source --provider Simulated` produce a real PASS/FAIL
table with no bench.

Cleanest coupling = **decorate the DUT** so it captures the exact set-points the verifier commands, and push
them into **seeded reference sim devices**. Provider-agnostic; needs **no change to any existing sim class**
(it only writes their public settable properties). Coupling is **exact** (measured == commanded → PASS at any
tolerance) — that must be documented in the output; injecting residual error is a future nicety.

New file: **`src/GpibUtils.Console/Instruments/SimulatedHarnessBench.cs`**

```csharp
internal sealed class SimulatedBench            // the shared "what the DUT is currently emitting" state
{
    public double CarrierFrequencyHz;
    public double CarrierPowerDbm;
    public double SourceVolts;
}

internal sealed class SimReferenceRequest       // one reference the harness is about to open
{
    public string Resource { get; }             // already resolved via store.Resolve(...)
    public ReferenceChoice Choice { get; }
}

internal sealed class SimReferenceModel
{
    public SimulatedInstrument Instrument { get; }
    public Action<SimulatedBench> PowerSync, FreqSync, VoltSync;   // settable; null = role unsupported
    public Action<SimulatedBench> SyncFor(ReferenceQuantity q);    // switch → the matching sync
}

internal sealed class SimulatedBenchCoupling
{
    public SimulatedBench Bench { get; }
    public IReadOnlyList<string> Warnings { get; }
    public void Apply();                                   // invoke every registered role sync
    public ISignalSource Couple(ISignalSource s);           // → BenchCouplingSource
    public IVoltageSourceDut Couple(IVoltageSourceDut d);   // → BenchCouplingVoltageSource
}

internal static class SimulatedHarnessBench
{
    // registry: catalog key → factory(bench) → model
    public static SimulatedBenchCoupling TrySeed(IGpibProvider provider, IEnumerable<SimReferenceRequest> refs);
}
```

`TrySeed` returns `null` unless `provider is SimulatedGpibProvider`. It **dedupes by resolved resource** (the
same instrument filling both the power and frequency role → ONE seeded device, both role syncs registered),
calls `sim.Add(resource, model.Instrument)`, and collects warnings. If two *different* catalog keys resolve to
the same resource, warn — only the first is simulated.

**Seeding must happen BEFORE the references are opened**, otherwise `AutoCreate` registers a generic first.

Decorators:

```csharp
BenchCouplingSource : ISignalSource
    SetFrequencyMHz(mhz) → bench.CarrierFrequencyHz = mhz * 1e6; inner.SetFrequencyMHz(mhz); coupling.Apply();
    SetPowerDbm(dbm)     → bench.CarrierPowerDbm   = dbm;        inner.SetPowerDbm(dbm);      coupling.Apply();
    // forward ResourceName / Initialize / Preset / RfOn / RfOff

BenchCouplingVoltageSource : IVoltageSourceDut
    SetVolts(v) → bench.SourceVolts = v; inner.SetVolts(v); coupling.Apply();
    // forward DisplayName / EnableOutput / DisableOutput
```

### Wiring

- `SourceHarness.RunSignalSource` — used by **both** `verify harness` (interactive) and `verify source`
  (one-shot). Precompute the resolved resources with `store.Resolve(...)` up front, `TrySeed` before opening
  the references, then wrap the opened DUT (`dut.Open(dutSession)`) when `coupling != null`. The existing
  `IInstrumentSession Open(addr,key,def)` local function becomes redundant — drop it.
- `SourceHarness.RunDcInteractive` — same shape with `BenchCouplingVoltageSource`.
- Print a banner whenever coupling is active, e.g. *"Simulated bench: references are seeded with their device
  models and read back the DUT's commanded set-point exactly, so every graded point PASSes by construction.
  Use it to exercise the harness, not to judge an instrument."* — plus any warnings.

`GpibUtils.Console` already references every assembly needed (Meters / Counters / Analyzers) plus
`GpibUtils.Visa` (which holds `GpibUtils.Visa.Simulation`). `MeasurementFunction` is public in
`GpibUtils.Instruments.Meters`.

## 4. Reading-property map — **verified against the simulator source, 2026-07-23**

> Three entries here **correct** an earlier draft of this map. Do not trust the older version; the corrections
> are called out in bold and each was read out of the driver/simulator source.

| Catalog key | Simulated device (namespace) | Role sync |
|---|---|---|
| `e4418b` | `HpE4418BSimulatedDevice` (Meters) | Power: `.PowerDbm = b.CarrierPowerDbm` |
| `hp438a` | `Hp438ASimulatedDevice` (Meters) | Power: `.PowerDbmA = b.CarrierPowerDbm` |
| `hp8902a` | `Hp8902ASimulatedDevice` (Meters) | **special — see below** |
| `hp8560e` | `Hp8560ESimulatedDevice` (Analyzers) | Power: **`.Trace = new[]{ b.CarrierPowerDbm }`**; Freq: `.MarkerFrequencyHz = b.CarrierFrequencyHz` |
| `hp8591e` | `Hp8591ESimulatedDevice` (Analyzers) | same as 8560E |
| `hp53131a` | `Hp53131ASimulatedDevice` (Counters) | Freq: `.Frequency = b.CarrierFrequencyHz` |
| `hp5342a` | `Hp5342ASimulatedDevice` (Counters) | Freq: `.Frequency = b.CarrierFrequencyHz` |
| `hp5351a` | `Hp5351ASimulatedDevice` (Counters) | Freq: `.Frequency = b.CarrierFrequencyHz` |
| `hp34401a` | `Hp34401ASimulatedDevice` (Meters) | Volts: `.Reading = b.SourceVolts` |
| `hp3458a` | `Hp3458ASimulatedDevice` (Meters) | Volts: `.Reading = b.SourceVolts` |
| `dm3058` | `RigolDm3058SimulatedDevice` (Meters) | Volts: `.SetReading(MeasurementFunction.DcVoltage, b.SourceVolts)` |
| `hp437b`, `hp436a`, `keithley2015`, `hp5343a` | **none exists** | warn "no simulated model; may not respond under Simulated", fall through to the generic instrument |

### Correction 1 — the 8560E / 8591E marker amplitude cannot be set directly

`Hp8560E.MarkerToPeakAmplitude()` sends `MKPK HI` then queries `MKA?`, and the simulator's `Apply` does:

```csharp
if (upper.StartsWith("MKPK")) { MarkerAmplitudeDbm = Trace != null && Trace.Length > 0 ? Trace.Max() : MarkerAmplitudeDbm; }
```

so a synced `MarkerAmplitudeDbm` is **overwritten at read time** by the peak of `Trace`. The power sync must
therefore seed **`Trace`** (setting `MarkerAmplitudeDbm` as well is harmless belt-and-braces).
`MarkerFrequencyHz` is *not* touched by `MKPK`, so the frequency sync writes it directly.

### Correction 2 + 3 — the 8902A's units, and its dual role

The 8902A simulator answers **every** settled read from a single `Reading` property, and the driver converts
per mode:

- `ReadRfPowerDbm()` → `Rf.WattsToDbm(ReadMeasurement())` → **`Reading` must be in WATTS**, i.e.
  `Rf.DbmToWatts(b.CarrierPowerDbm)`. (`Rf` lives in `GpibUtils.Instruments.Meters`; `DbmToWatts` exists.)
- `ReadSignalFrequencyMHz()` → sends `M5`, reads Hz, returns `hz / 1e6`; and
  `MeasuringReceiverFrequencyReference.Measure()` multiplies by `1e6` again → **`Reading` must be in Hz**,
  i.e. `b.CarrierFrequencyHz` (**not** `/1e6`).

Because those two roles need *different units in the same property*, a set-point-time sync cannot serve both
when one 8902A fills both roles (it is offered in both the power and the frequency list). Resolve it at
**read time** instead — the simulator exposes `Mode` publicly (`private set`), and it is `"M5"` for frequency,
`"M4"`/`"S4"` for power:

```csharp
private static SimReferenceModel Hp8902AModel(SimulatedBench bench)
{
    var d = new Hp8902ASimulatedDevice();
    var inner = d.Instrument.Responder;                 // the model's own responder
    d.Instrument.Responder = cmd =>
    {
        // ONE Reading property, units depend on mode → resolve at read time so the same receiver can
        // fill both the power and the frequency role in a single run.
        d.Reading = d.Mode == "M5" ? bench.CarrierFrequencyHz : Rf.DbmToWatts(bench.CarrierPowerDbm);
        return inner(cmd);
    };
    return new SimReferenceModel(d.Instrument);          // no set-point syncs needed
}
```

Consequence: `TrySeed` must return a coupling when **anything was seeded**, not only when syncs were
registered (the 8902A registers none) — the DUT decorator still has to update `bench` for the closure to read.

### Other behaviour confirmed while designing

- `HpE4418BSimulatedDevice` raises the Event-Summary bit on any write containing `*OPC`, so
  `PowerMeterReference.Prepare`'s one-time `ZeroAndCalibrate()` completes. `FETCH?` returns `PowerDbm` in dBm.
- `Hp8560ESimulatedDevice` drives the #43 SRQ-edge `CompletionWaiter` via `OnSerialPoll` (`BusyPolls = 1`,
  `SweepCompletes = true` by default), so `SingleSweep()` completes.
- `SimulatedInstrument.Responder` / `WriteObserver` / `OnSerialPoll` are all public settable — wrapping is fine.
- The DUT address itself is deliberately **not** seeded; the generic instrument absorbs source writes. If a
  signal-source driver turns out to query during `Open`, seed it too (verify in the smoke test).

## 5. Also-do before merging #174

- **Fix build warning CS1734** in `src/GpibUtils.Verification/References/RfPowerReferences.cs` ~line 79: the
  `PowerMeterReference` **class** `<summary>` uses `<paramref name="setFrequencyMHz"/>`, which is invalid on a
  type → change to `<c>setFrequencyMHz</c>`.
- **Correct the over-claiming wording.** `Fully drivable hardware-free against the Simulated provider` appears
  in `VerifyHarnessCommands.cs` (`VerifyHarnessCommand` class docstring) and, as
  `so a whole verification is drivable hardware-free against the Simulated provider`, in
  `References/ReferenceMeasurement.cs` (`IReferenceMeasurement` docstring). Also in the repo `HANDOFF.md` on
  `main`. Reword to describe what is actually true once this lands: an ideal, PASS-by-construction demo bench.
- Update `docs/VERIFICATION_HARNESS.md` to describe the simulated coupling.
- Optional: a small Verification/Console test for the coupling.

## 6. Then

1. Build → `dotnet test` the Verification tests (expect 36/36).
2. Smoke: `verify source --provider Simulated --dut hp8340b --power-ref e4418b --freq-ref hp53131a
   --points 100@0,500@0,1000@-10 --power-tol-db 1 --freq-tol-ppm 1` → expect an all-PASS table, no hangs.
   Also smoke the 8902A in **both** roles at once, and `verify harness` interactively.
   Smoke-test exe: `src/GpibUtils.Console/bin/Debug/net472/gpibutils.exe`.
3. Commit to the #174 branch → **merge #174, then #175**.
4. Per the no-hardware policy: update `SharedMemory.md`, add rows to board **#46**, close any issue the PRs
   resolve. (Software-only work has no bench gate.)

## 7. Admin-only steps still pending (cannot be done from a PR — for Tony)

1. **Branch protection** on `main` → require the `build-and-test` check (Settings → Branches) so CI becomes a
   merge gate.
2. **Network policy** for the web environment → allow `dot.net` and `builds.dotnet.microsoft.com` so the
   SessionStart hook can install the SDK (currently 403). Alternative: bake the SDK into a custom image.
