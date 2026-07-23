# CI & cloud dev environment

Two layers of "cloud build" for GPIBUtils-NG, and what you need to do to make each one *proper*.

| Layer | Where it runs | Purpose |
|---|---|---|
| **GitHub Actions CI** | GitHub-hosted `windows-latest` | The authority. Builds the **full** solution (incl. the Windows-only WPF shell) and runs every test on each PR/push. |
| **Session-start toolchain** | Claude Code on the web container (Linux) | Lets a coding session build & run the Simulated-green tests **before** pushing, so mistakes are caught locally instead of burning a CI round. |

## 1. GitHub Actions CI (`.github/workflows/ci.yml`)

The pipeline already existed; it is now hardened:

- **`permissions: contents: read`** — least-privilege token.
- **`concurrency` with `cancel-in-progress`** — superseded runs on the same ref are cancelled.
- **NuGet cache** — `~/.nuget/packages` keyed on the project files, so restore is near-instant when
  dependencies are unchanged.
- **Test results** — `dotnet test --logger trx`, uploaded as a `test-results` artifact **even on failure**, so
  a red build shows which tests failed without scraping the log.

The build stays on **`windows-latest`** because `net472` + the WPF front-end require the .NET Framework /
Windows. No OS matrix is needed (single TFM).

### What *you* still need to do (one-time, in repo Settings — not YAML)

To make CI a real gate rather than advisory:

1. **Settings → Branches → Add branch protection rule** for `main`.
2. Enable **Require a pull request before merging**.
3. Enable **Require status checks to pass** and select the **`build-and-test`** check.
4. (Optional) **Require branches to be up to date before merging**, and **Do not allow bypassing**.

Until this is set, CI runs but nothing stops a red merge. (This step needs repo-admin rights, so it is a
manual toggle rather than something the automation can push.)

## 2. Session-start toolchain (`.claude/hooks/session-start.sh`)

A **SessionStart hook** (registered in `.claude/settings.json`) provisions the .NET 10 SDK into `~/.dotnet`
on Claude Code on the web sessions, persists it on `PATH` for the session, and warms a `restore`. It is
idempotent, non-interactive, and **remote-only** (a no-op when `CLAUDE_CODE_REMOTE` isn't `true`). A failed
install is **non-fatal** — the session still starts with a clear message.

Because the Windows-only WPF projects can't build on Linux, the hook targets a **solution filter that
excludes them**, [`GPIBUtils-NG.NoWpf.slnf`](../GPIBUtils-NG.NoWpf.slnf) (52 of the 54 projects). In a
session with the SDK available:

```
dotnet build GPIBUtils-NG.NoWpf.slnf -c Release
dotnet test  GPIBUtils-NG.NoWpf.slnf -c Release
```

The full solution (incl. WPF) is validated by CI on Windows.

### What *you* need to do: allow the SDK download in the network policy

`dotnet-install.sh` fetches from **`dot.net`** and **`builds.dotnet.microsoft.com`**. The environment's
network policy must permit outbound HTTPS to those hosts. On a **restricted** policy they are blocked — the
hook prints a warning and the session continues without `dotnet` (verified: the current environment returns
`403 … builds.dotnet.microsoft.com`). To enable local builds, pick one:

- **Use / configure a network policy that allows** `dot.net` and `builds.dotnet.microsoft.com` (plus
  `dotnetcli.blob.core.windows.net` for some channels), then the hook installs the SDK automatically. See
  the [environment docs](https://code.claude.com/docs/en/claude-code-on-the-web).
- **Bake the SDK into a custom environment image** (pre-install .NET 10) so no download is needed at
  session start — the most robust option for a locked-down policy.

### Sync vs async

The hook runs **synchronously**: the toolchain is guaranteed ready before the first agent turn (no race
where a build/test runs before the SDK is present), at the cost of a slightly slower session start. Switch it
to async (`echo '{"async": true, "asyncTimeout": 300000}'` as the first line) if you'd rather have faster
startup and can tolerate the SDK not being ready for the very first command.

> Once this branch merges to the default branch, **all future web sessions pick up the hook.**
