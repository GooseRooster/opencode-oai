#!/usr/bin/env bash
# Baseline repo setup for a .NET dev container — runs before setup-local.sh.
set -euo pipefail

# Restore /tmp to sticky world-writable (1777). Feature build steps can leave it
# 0755 root-owned; under keep-id (non-root 'ubuntu') that breaks every dotnet/MSBuild
# call (they create temp dirs under Path.GetTempPath() == /tmp). 'ubuntu' has sudo.
sudo chmod 1777 /tmp

# devcontainers/features/common-utils:2 has a regression where it creates
# (or recreates) ~/.local and ~/.config as root-owned after install, which
# breaks anything that subsequently tries to mkdir under them -- notably
# chezmoi (`chezmoi init` fails with "permission denied" on ~/.local/share/chezmoi),
# cargo, and any tool that lazy-creates XDG dirs. The fix has to run here in
# post-create: the Feature runs between the Dockerfile and us, so a Dockerfile
# chown would just be clobbered. Idempotent; ignores paths that don't exist yet.
sudo chown -R "$(id -u):$(id -g)" "$HOME/.local" "$HOME/.config" 2>/dev/null || true

# The read-only NuGet.Config bind-mount makes podman auto-create ~/.nuget as root,
# which would make the package cache (~/.nuget/packages) unwritable. Re-own the
# dirs (leaving the read-only Config file itself untouched). No-op if unmounted.
if [ -e "$HOME/.nuget/NuGet/NuGet.Config" ]; then
  sudo chown ubuntu:ubuntu "$HOME/.nuget" "$HOME/.nuget/NuGet"
fi

# Restore. TODO: point at your solution/project.
# dotnet restore YourSolution.slnx

# JetBrains ReSharper command-line tools — provides `jb cleanupcode` for reformatting
# stubborn files the LSP can't fix (see scripts/dev.nu's `format` command, if present).
dotnet tool install --global JetBrains.ReSharper.GlobalTools

# ASP.NET Core dev HTTPS cert: export a STABLE PEM cert+key into a gitignored,
# host-visible dir (persists across rebuilds -> trust once). Kestrel is pointed at
# these via ASPNETCORE_Kestrel__Certificates__Default__* (devcontainer.json), so the
# served cert matches what the host trusts. See README.md ("Trusting the dev cert").
CERT_DIR="$PWD/.devcontainer/.certs"
if [ ! -f "$CERT_DIR/localhost.pem" ]; then
  mkdir -p "$CERT_DIR"
  dotnet dev-certs https --export-path "$CERT_DIR/localhost.pem" --format PEM --no-password
fi
