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

_No instrument drivers have merged yet — the foundation (#1) is transport-only and needs no hardware.
The first driver to land will appear here as 🟡 Verification Needed._

| Issue | Instrument | Verify tag | HW required | Status | FW / serial | Date | Notes |
|---|---|---|---|---|---|---|---|
| — | _(none yet)_ | — | — | — | — | — | — |

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
