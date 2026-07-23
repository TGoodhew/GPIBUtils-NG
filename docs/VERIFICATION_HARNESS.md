# Cross-instrument verification harness

Verify a **device under test (DUT)** by measuring its real output with a **reference instrument** — and,
whenever more than one instrument can do the job, **pick which one to use**. This generalizes the original
plan-driven Fluke-5440 runner (issue [#37](https://github.com/TGoodhew/GPIBUtils-NG/issues/37)) into a
reusable framework so the same pattern verifies signal generators, calibrators and power supplies.

The classic example: **use an HP 8902A measuring receiver to verify a signal generator** — the 8902A reads
the generator's actual RF power and frequency, the harness grades each point PASS/FAIL. But an 8902A is not
the only instrument that can read RF power, so the harness offers the alternatives (a power meter) and lets
you choose.

## What can verify what

| DUT category | Quantity | Reference instruments you can choose between |
|---|---|---|
| Signal generator / CW source (`ISignalSource`) | RF power (dBm) | HP 8902A · HP E4418B · HP 438A · HP 437B · HP 436A · HP 8560E · HP 8591E |
| Signal generator / CW source | Frequency (Hz) | HP 53131A · HP 5351A · HP 5342A · HP 5343A · HP 8902A · HP 8560E · HP 8591E |
| DC calibrator / power supply (`IVoltageSourceDut`) | DC volts (V) | HP 34401A · Rigol DM3058 · HP 3458A · Keithley 2015 |

(The 8560E/8591E spectrum analyzers read the peak marker — a lower-accuracy but bench-available
alternative to a power meter/receiver or a counter.)

The lists live in one place — `GpibUtils.Verification.Catalog.VerificationCatalog` — so adding a reference is
one entry, and both the interactive and one-shot front-ends pick it up.

## Architecture

```
DUT driver ─────────────┐                        ┌───────── reference driver(s)
(ISignalSource,         │   SignalSourceVerifier │   IRfPowerReference   (8902A, power meters)
 IVoltageSourceDut)     ├──► / DcSourceVerifier ──┤   IFrequencyReference (counters, 8902A)
                        │   (plan → PASS/FAIL)    │   IDcVoltageReference (DMMs)
VerificationCatalog ────┘                        └───────── each wraps a driver over the shared VISA transport
```

- **`GpibUtils.Verification`** (no UI dependency) holds the interfaces, the driver adapters, the two runners
  and the catalog.
- **`GpibUtils.Console`** holds the Spectre.Console front-end.

Every reference is an adapter over an existing driver — **no driver changed**.

> ### ⚠️ The harness needs real instruments today
>
> A verification run is **not** yet drivable end-to-end against the `Simulated` provider: it errors or
> stalls. `SimulatedGpibProvider.Open` auto-creates a **generic** `SimulatedInstrument` for any
> unregistered address, and the specific `*SimulatedDevice` models are only ever instantiated inside unit
> tests — no front-end seeds them. A generic instrument never raises the status bits the reference drivers'
> completion handshakes wait on, so those drivers correctly time out.
>
> This is **not** specific to the harness — the whole CLI's Simulated mode is generic. The harness just
> surfaces it sharply, because its references (correctly) wait on SRQ/OPC completion.
>
> The unit tests are hardware-free and green; they use fakes rather than the provider. The `--provider
> Simulated` examples below therefore illustrate **command shape**, not a working demo. Making the harness
> a genuine no-hardware demo bench is fully designed in [`SIM_BENCH_PLAN.md`](SIM_BENCH_PLAN.md).

## Interactive harness

```
gpibutils verify harness --provider Simulated
```

Walks you through: provider → DUT → which quantities to verify → **which reference instrument** for each
(a menu appears whenever more than one can do it) → instrument addresses → the plan → a PASS/FAIL table.

## One-shot (scripting / CI — UI parity)

Verify an HP 8340B's output power on an 8902A, ±1 dB, at two frequencies:

```
gpibutils verify source --dut hp8340b --power-ref hp8902a \
    --points "1000@0, 2000@-10" --power-tol-db 1 --provider Simulated
```

Add a frequency check on a 53131A (±5 ppm) and write a CSV:

```
gpibutils verify source --dut hp8673b --power-ref e4418b --freq-ref hp53131a \
    --points "2000@-10, 8000@-10" --power-tol-db 1.5 --freq-tol-ppm 5 --csv out.csv
```

Points are `freqMHz@powerDbm`, comma/semicolon separated. Exit code is **1 if any point FAILs** its tolerance
(0 otherwise), so it drops straight into a script or CI gate. Instrument addresses come from
`--dut-addr` / `--power-addr` / `--freq-addr`, else the configured address book, else the driver default.

The existing `gpibutils verify 5440` one-shot (calibrator → 34401A) is unchanged; the interactive harness
covers the same DC-source case plus power supplies via a selectable DMM reference.

## Notes / limitations

- RF-power references measure directly; on real hardware the 8902A/power-meter zero+calibrate against the
  50 MHz reference is a manual bench step (do it before running for absolute accuracy).
- Sources that do not implement `ISignalSource` (e.g. HP 8350B has no RF on/off, HP 3335A is listen-only) are
  not offered as DUTs yet.
- All results are **Simulated-green** for CI; real-hardware confirmation follows the
  [`HARDWARE_VERIFICATION.md`](HARDWARE_VERIFICATION.md) workflow.
