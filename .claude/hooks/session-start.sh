#!/bin/bash
# SessionStart hook — provision the .NET SDK so Claude Code on the web can build
# and run the Simulated-green tests before pushing (CI on Windows remains the
# authority for the full solution, incl. the Windows-only WPF projects).
#
# Runs synchronously so the toolchain is ready before the first agent turn.
# Idempotent and non-interactive; the container state is cached after it
# completes, so a re-run is a fast no-op.
set -euo pipefail

# Only provision in Claude Code on the web (remote) sessions; a local machine
# is assumed to already have the SDK.
if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

DOTNET_DIR="${DOTNET_ROOT:-$HOME/.dotnet}"
DOTNET_CHANNEL="10.0"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

# Install the SDK once. dotnet-install.sh reaches dot.net / builds.dotnet.microsoft.com,
# so the environment's network policy must allow those hosts (see docs/CI_AND_DEV_ENVIRONMENT.md).
# Failure is non-fatal: the session still starts, with a clear message, so a missing
# network allowlist degrades gracefully instead of blocking startup.
if ! "$DOTNET_DIR/dotnet" --version >/dev/null 2>&1; then
  echo "session-start: installing .NET SDK ($DOTNET_CHANNEL) into $DOTNET_DIR ..." >&2
  if curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
       && bash /tmp/dotnet-install.sh --channel "$DOTNET_CHANNEL" --install-dir "$DOTNET_DIR"; then
    echo "session-start: .NET SDK installed." >&2
  else
    echo "session-start: WARNING — could not install the .NET SDK." >&2
    echo "  The environment's network policy must allow dot.net and" >&2
    echo "  builds.dotnet.microsoft.com. See docs/CI_AND_DEV_ENVIRONMENT.md." >&2
    echo "  Session continues without dotnet; CI (Windows) remains the build gate." >&2
    exit 0
  fi
fi

# Persist the toolchain on PATH for the rest of the session.
if [ -n "${CLAUDE_ENV_FILE:-}" ]; then
  {
    echo "export DOTNET_ROOT=\"$DOTNET_DIR\""
    echo "export PATH=\"$DOTNET_DIR:\$PATH\""
    echo "export DOTNET_CLI_TELEMETRY_OPTOUT=1"
    echo "export DOTNET_NOLOGO=1"
    echo "export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1"
  } >> "$CLAUDE_ENV_FILE"
fi

export PATH="$DOTNET_DIR:$PATH"

# Warm the restore for the Linux-buildable project set (everything except the
# Windows-only WPF projects) so the first build is fast. Best-effort.
if [ -f "${CLAUDE_PROJECT_DIR:-.}/GPIBUtils-NG.NoWpf.slnf" ]; then
  "$DOTNET_DIR/dotnet" restore "${CLAUDE_PROJECT_DIR:-.}/GPIBUtils-NG.NoWpf.slnf" >&2 || \
    echo "session-start: restore failed (non-fatal); run it manually once the SDK is available." >&2
fi

echo "session-start: .NET toolchain ready — build/test with GPIBUtils-NG.NoWpf.slnf" >&2
