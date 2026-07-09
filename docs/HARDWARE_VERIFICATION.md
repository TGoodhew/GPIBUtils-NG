# Hardware verification status

**Master status board for real-hardware verification.** This is the portable, in-repo mirror of the
pinned GitHub tracking issue [#46](https://github.com/TGoodhew/GPIBUtils-NG/issues/46). Because development currently happens **without access to the physical
instruments**, drivers are merged to `main` on simulator/unit-test green and then wait here for
bench confirmation.

## The no-hardware workflow

1. **Branch** per issue: `feat/<issue#>-<slug>` (e.g. `feat/6-hp11713a`).
2. **Build it hardware-free** — port the driver onto the shared transport, add a simulator/mock and
   unit tests, and wire the CLI branch (issue #45). It must build and pass tests under the
   **Simulated** provider.
3. **Merge to `main`** on simulator/unit-test green. **HW verification is _not_ a merge gate** — this
   is what lets the next driver build on it immediately while away from the bench.
4. **Do _not_ close the issue.** Instead:
   - Label it **`verification-needed`**.
   - Add the bench checklist (below) to the issue.
   - Add a row to the table here and to the pinned tracking issue.
   - Tag the merge commit **`verify/<issue#>-<instrument>`** so the exact merged state can be checked
     out at the bench later, even after `main` has advanced.
5. **At the bench** (hardware available): run the checklist against the real instrument, record the
   result here and on the issue (pass/fail, firmware/serial, date, notes).
6. **Close** the issue only after a ✅ pass — or open a follow-up if hardware reveals a discrepancy.

## Status legend

| State | Meaning |
|---|---|
| ⬜ Not built | No driver code merged yet. |
| 🟡 Verification Needed | Merged to `main`, simulator/tests green, awaiting real hardware. |
| ✅ HW-Verified | Confirmed against the physical instrument. |
| ⚠️ Discrepancy | Hardware behaviour differed from the simulator/port — see notes / follow-up. |

## Board

| Issue | Instrument | Verify tag | HW required | Status | FW / serial | Date | Notes |
|---|---|---|---|---|---|---|---|
| [#6](https://github.com/TGoodhew/GPIBUtils-NG/issues/6) | HP 11713A Attenuator/Switch Driver | `verify/6-hp11713a` | 11713A + step attenuators (8494/8496), GPIB | 🟡 Verification Needed | — | — | Merged simulator-green; 25 unit tests. Confirm relay data strings against real hardware. |
| [#7](https://github.com/TGoodhew/GPIBUtils-NG/issues/7) | HP 8340B Synthesized Sweeper | `verify/7-hp8340b` | 8340B, GPIB; power meter / counter to confirm output | 🟡 Verification Needed | — | — | Merged simulator-green; 14 unit tests. Confirm CW frequency / power / RF gating on real output. |
| [#8](https://github.com/TGoodhew/GPIBUtils-NG/issues/8) | HP 8673B Synthesized Signal Generator | `verify/8-hp8673b` | 8673B, GPIB; counter / power meter (2-26.5 GHz) | 🟡 Verification Needed | — | — | Merged simulator-green; 13 unit tests. Confirm FR/LE mnemonics + RF gating across the band on real output. |

## Per-instrument bench checklist (template)

Copy this into the issue when it moves to 🟡 Verification Needed, and fill it in at the bench:

```
### HW verification — <instrument> (issue #<n>)
Environment: provider <NI-VISA|…>, address <GPIB0::N::INSTR>, firmware <…>, serial <…>, date <YYYY-MM-DD>

- [ ] Discovery / *IDN? returns the expected model
- [ ] Each CLI command drives the instrument as designed (list them)
- [ ] Readings match the simulator's assumptions (units, scaling, engineering format)
- [ ] SRQ / serial-poll completion behaves (if the driver uses it)
- [ ] Error/timeout paths handled gracefully (bad address, no response)
- [ ] No hardcoded addresses; `--provider` / `--address` / `--timeout` honoured

Result: ✅ pass  /  ⚠️ discrepancy (describe)  /  ❌ fail (describe)
```
