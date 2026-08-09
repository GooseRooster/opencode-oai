#!/usr/bin/env bash
# console-log-bridge.sh
# ---------------------------------------------------------------------------
# Make your app's logs show up in `docker logs` / `podman logs` / lazydocker,
# even though the app is launched separately (dotnet run, a debugger, npm start,
# ...) as a child of an exec session rather than as PID 1.
#
# The container runtime only captures PID 1's stdout, so an exec-launched app's
# output never reaches it. This bridge tails a log FILE your app writes and pumps
# it into PID 1's stdout.
#
# Provider-agnostic: point ANY logging provider at $CONSOLE_LOG_FILE (default
# /tmp/console-dev.log) -- an NLog file target, a Serilog File sink, Python
# logging.FileHandler, a plain `... | tee`, etc. ANSI colour codes in the file
# are preserved and render in lazydocker.
#
# PID 1 is root-owned, so the redirect into /proc/1/fd/1 runs under the
# container's passwordless sudo. Idempotent: a no-op if already bridging the file.
# ---------------------------------------------------------------------------
set -euo pipefail

LOG_FILE="${CONSOLE_LOG_FILE:-/tmp/console-dev.log}"

if [ "${DEVCONTAINER:-}" != "true" ]; then
  echo "console-log-bridge: DEVCONTAINER != true; nothing to bridge. Skipping."
  exit 0
fi

# Already tailing this file? Done -- don't restart it (that would briefly unlink a
# file the app may be actively writing). A fresh container has no prior tail.
# Anchor with ^tail so the pattern can't match this check's own `sudo pgrep ...`
# command line (which literally contains the pattern string).
if sudo pgrep -f "^tail -F $LOG_FILE" >/dev/null 2>&1; then
  echo "console-log-bridge: already running for $LOG_FILE."
  exit 0
fi

# Clear any stale file, then let the APP recreate it so it's owned by whoever the app
# runs as (and can therefore write it). We must NOT create it here: a file created by
# this script (or a prior root run) with the wrong owner is unwritable by the app in a
# sticky /tmp. sudo because the stale file may be root-owned. `tail -F` waits for it.
sudo rm -f "$LOG_FILE"

# Pump as root in a detached session; the redirect is opened by root's shell (a
# redirect written by our non-root shell would be opened as us and denied).
sudo sh -c "setsid tail -F '$LOG_FILE' >> /proc/1/fd/1 2>&1 &"

echo "console-log-bridge: streaming $LOG_FILE -> PID 1 stdout (docker/podman logs)."
