# .NET dev container

The base skeleton (rootless-podman keep-id, `/tmp` fix, Homebrew-on-PATH, SSH agent
forwarding, gitignored `local/` hook) plus .NET SDK, NuGet private-feed auth, and a
**trust-once dev HTTPS certificate**. Open with VS Code or `devcontainer up`.

## What's inside
- .NET 10 SDK (pin the tag in `Dockerfile` to your target version)
- Runs as non-root `ubuntu` (uid 1000) under `--userns=keep-id`; `--network=host` on
- SSH agent forwarding (no private keys in the container)
- NuGet private-feed auth via your host's global `NuGet.Config`
- Stable dev HTTPS cert served on `:5001`, trustable by the host once
- Optional Blazor-WASM extras (wasm-tools, sass, headless Chrome) — see the
  `⟨blazor⟩` TODO blocks in `Dockerfile` and `scripts/setup-repo.sh`

## Host prerequisites
- **SSH agent** with your git key loaded, and `SSH_AUTH_SOCK` set in the shell you
  launch from (`ssh-add -l` to check).
- **NuGet PAT** in `~/.nuget/NuGet/NuGet.Config` (mounted read-only for restore).
  Create it before first launch, or the mount has no source.

## Trusting the dev HTTPS certificate
`setup-repo.sh` exports a stable cert to `.devcontainer/.certs/localhost.pem`
(+ `localhost.key`) — gitignored, but visible on your host through the workspace.
Kestrel serves exactly that cert on `:5001`, so trusting `localhost.pem` **once**
makes `https://localhost:5001` green. No dotnet needed on the host. (Covers
`localhost` only; delete `.certs/` and you'll re-trust a fresh one.)

**Windows** (per-user, no admin — Chrome/Edge use this store; reach the file via
`\\wsl.localhost\<distro>\...\.devcontainer\.certs\localhost.pem`):

    Import-Certificate -FilePath "<path>\localhost.pem" -CertStoreLocation Cert:\CurrentUser\Root

Or double-click `localhost.pem` → Install Certificate → Current User → "Trusted
Root Certification Authorities".

**Linux host** — system store (curl, .NET, etc.):

    sudo cp .devcontainer/.certs/localhost.pem /usr/local/share/ca-certificates/devcert-localhost.crt
    sudo update-ca-certificates

Chromium/Chrome/Edge use their own NSS store (needs `libnss3-tools`):

    certutil -d sql:$HOME/.pki/nssdb -A -t "C,," -n "dev localhost" -i .devcontainer/.certs/localhost.pem

(Firefox has a separate store — import via Settings → Certificates if you use it.)

## Personalization (optional)
Opt-in and never committed — see [`local.example/README.md`](local.example/README.md).
The personal setup also installs the `lazydotnet` / `dotnet-outdated` CLIs.

## Files
- `devcontainer.json` — environment definition (user/keep-id/network, mounts, Kestrel cert env, ports)
- `Dockerfile` — .NET SDK image (+ optional Blazor extras)
- `scripts/setup-repo.sh` — baseline setup (`/tmp` + nuget fixups, restore, dev-cert export)
- `scripts/setup-local.sh` — runs your gitignored `local/setup.sh` if present
- `local.example/` — template for personal setup
- `.gitignore` / `.gitattributes` — nested, keep the template self-contained + LF-safe
