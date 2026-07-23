# Session handoff — verification harness + cloud CI/dev environment

Handoff from a **Claude Code on the web** session to local work (e.g. Claude Code in VS Code).
All work is pushed to GitHub as two open **draft** PRs; the chat transcript does not migrate, so this
file is the durable context. Once these PRs merge and the branches are gone, this file can be deleted.

## Open work

### PR #174 — Cross-instrument verification harness
Branch: `claude/instrument-verification-harness-z7phx1`

Verify a **device under test** by measuring its real output with a **reference instrument**, and — when
more than one instrument can do the job — **let the user pick between them** (e.g. an HP 8902A *or* a power
meter *or* a spectrum analyzer to check a signal generator).

- `GpibUtils.Verification` (no UI dependency):
  - **Reference-measurement layer** (`References/`): `IReferenceMeasurement` + `ReferenceQuantity`
    (RfPowerDbm / FrequencyHz / DcVolts), and adapters wrapping existing drivers —
    `MeasuringReceiverPowerReference`/`…FrequencyReference` (8902A), `PowerMeterReference`
    (E4418B/438A/437B/436A), `SpectrumAnalyzerPowerReference`/`…FrequencyReference` (8560E/8591E),
    `FrequencyCounterReference` (53131A), `LegacyCounterReference` (5342A/5343A/5351A),
    `DmmVoltageReference` (34401A/DM3058/Keithley 2015), `Hp3458AVoltageReference`.
  - **Runners**: `SignalSourceVerifier` (drives any `ISignalSource`; grades power in dB, frequency in ppm)
    and `DcSourceVerifier` (a calibrator *or* power supply via `IVoltageSourceDut`; ppm).
  - **`Catalog/VerificationCatalog`**: the single source of truth mapping each DUT category → the
    reference instruments that can verify each quantity (this is what drives the "selectable reference" UX).
- `GpibUtils.Console`: interactive **`verify harness`** (Spectre.Console — pick DUT, then reference(s),
  menu appears when >1 can do it) and one-shot **`verify source`** (CLI parity, exit 1 on FAIL). The
  existing `verify 5440` is unchanged.
- Docs: `docs/VERIFICATION_HARNESS.md`. Tests: `tests/GpibUtils.Verification.Tests` (fakes + sim-green
  integration + catalog smoke). No driver changes.

**Status at handoff:** CI green on the harness commit; a follow-up commit added the spectrum-analyzer and
extra-DMM references — confirm that run is green.

### PR #175 — Cloud CI / dev environment
Branch: `claude/ci-cloud-environment`

- **Hardened `.github/workflows/ci.yml`**: least-privilege `permissions: contents: read`, `concurrency`
  with `cancel-in-progress`, NuGet package caching, and `.trx` test results uploaded as an artifact.
- **SessionStart hook** (`.claude/hooks/session-start.sh` + `.claude/settings.json`): remote-only,
  idempotent; installs the .NET 10 SDK so **Claude Code on the web** can build/test before pushing.
  Non-fatal on failure. (It is a no-op locally — guarded by `CLAUDE_CODE_REMOTE` — so VS Code is unaffected.)
- **`GPIBUtils-NG.NoWpf.slnf`**: the 52 non-WPF projects, the Linux-buildable target (the WPF shell only
  builds on Windows).
- Docs: `docs/CI_AND_DEV_ENVIRONMENT.md`.

## Building & testing locally
- **Windows** (full solution incl. WPF): `dotnet build GPIBUtils-NG.sln` / `dotnet test GPIBUtils-NG.sln`.
- **macOS/Linux** (WPF excluded): `dotnet build GPIBUtils-NG.NoWpf.slnf` / `dotnet test GPIBUtils-NG.NoWpf.slnf`.
  > `GPIBUtils-NG.NoWpf.slnf` lives on PR #175's branch — check it out, or run the two WPF-excluded
  > projects individually until #175 merges.
- Everything is drivable hardware-free against the **Simulated** provider (`--provider Simulated`).

## Two admin-only steps still pending (can't be done from a PR)
1. **Branch protection** on `main` → require the `build-and-test` check (Settings → Branches). Needed to make
   CI an actual merge gate.
2. **Network policy** for the web environment → allow `dot.net` and `builds.dotnet.microsoft.com` so the
   SessionStart hook can install the SDK (currently blocked → 403). Or bake the SDK into a custom image.

## Suggested next steps
1. Confirm CI is green on both PRs; pull each branch and `dotnet build`/`dotnet test` locally.
2. Mark the PRs ready for review and merge (harness first, then CI/dev-env — or either order; they don't
   conflict).
3. Do the two admin steps above.
4. Optional follow-ups: add more DUT families to the harness (function generators via a counter; frequency
   counters verified by a reference source); add simulators for the Rigol DSA800 / Agilent N9320A analyzers
   so they can join the reference menus too.
