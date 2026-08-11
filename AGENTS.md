# AGENTS.md

Guidance for any AI coding agent (opencode, Claude Code, Cursor, etc.) working
in this repository. Human contributors will also benefit from skimming it.

## Repository at a glance

- **What.** OpenAI-compatible bridge (`/v1/models`, `/v1/chat/completions`)
  in front of a local OpenCode server. See [PROJECT.md](./PROJECT.md) for the
  detailed design.
- **Stack.** .NET 10, `Microsoft.NET.Sdk.Web`, `WebApplication.CreateSlimBuilder`,
  `PublishAot=true`, `IsAotCompatible=true`, `TreatWarningsAsErrors=true`.
- **Layout.**
  - `src/OpencodeOai/` — main project (namespace `OpencodeOai`)
  - `tests/OpencodeOai.Tests/` — xUnit + FluentAssertions + Moq
  - `opencode-oai.slnx` — solution
  - `Dockerfile`, `docker-compose.yml` — the intended deployment target
  - `deploy/quadlet/` — Podman Quadlet unit + env template (systemd service)

## Golden rules

1. **Preserve AOT compatibility.** No reflection-based configuration binding,
   no runtime code generation, no `JsonPolymorphic`, no reflection-based
   dependency scanning. Every serialised type must be declared in a
   `[JsonSerializable]` context. Every endpoint must be explicitly registered
   in `Endpoints/EndpointRegistry.cs`.
2. **Keep DTOs flat.** OpenCode's message parts are modelled as a single
   `PartDto` with all optional fields — don't reintroduce a class hierarchy.
3. **Do not add tool-calling support.** `tools`, `tool_choice`, `tool_calls`,
   and `role: "tool"` messages are intentionally dropped. This is a scope
   decision; see PROJECT.md for the rationale.
4. **Do not thread `x-working-directory` through session creation.** Sessions
   are created without a `directory` field on purpose (host↔container path
   mismatch when the editor runs in a devcontainer).
5. **Every commit should build cleanly and pass tests.** Zero warnings.

## Build & test — run inside the devcontainer

**Host tooling is unreliable.** In practice, native AOT publish on a developer
laptop tends to fail at the linker step because zlib / brotli / krb5 dev libs
aren't installed. Rather than debug that per-machine, run every build and test
command inside this repo's devcontainer, which pins the exact SDK image and
has the native prerequisites:

If the CLI is unavailable, attempt use of host tooling. If neither are available, stop and ask the user how to proceed.

```sh

# Bring the container up (idempotent):
devcontainer up --workspace-folder .

# Run anything inside it:
devcontainer exec --workspace-folder . dotnet build
devcontainer exec --workspace-folder . dotnet test
devcontainer exec --workspace-folder . dotnet run --project src/OpencodeOai
devcontainer exec --workspace-folder . dotnet publish src/OpencodeOai -c Release -r linux-x64
```

When `DEVCONTAINER=true` is set (the devcontainer sets this), the bridge also
mirrors JSON logs to `/tmp/console-dev.log` for easy tailing.

For containerised runs of the bridge itself, use the multi-stage `Dockerfile`
which does an AOT publish inside `mcr.microsoft.com/dotnet/sdk:10.0`:

```sh
docker compose up --build
```

## Commit conventions

- Semantic prefixes: `feat`, `fix`, `chore`, `docs`, `test`, `build`, `refactor`.
- Short imperative subject line, no scope prefix noise.
- **Do not add co-authorship trailers or attribution.** The maintainer handles
  that manually in the README.
- Amend / rebase freely before pushing; keep history readable.

## Testing expectations

- New behaviour → unit test in `tests/OpencodeOai.Tests/`.
- New endpoint → an integration test via `BridgeFactory` (stubs the OpenCode client).
- Prefer stubbing `IOpenCodeClient` over hitting a real server.
- No live-server tests in the default test run.

## When adding a new endpoint

1. Create `Endpoints/<Name>Endpoint.cs` implementing `IEndpoint`.
2. Add it to `Endpoints/EndpointRegistry.Endpoints`.
3. Any new response DTO → add to a `[JsonSerializable]` context.
4. Any auth-required endpoint → `.RequireAuthorization(ApiKeyAuthHandler.SchemeName)`.
5. Update `src/OpencodeOai/openapi.json` and, if relevant, the `.http` file.
6. Add an integration test.

## Known things you should not "fix"

- **Buffered streaming.** The bridge streams by awaiting the full upstream
  response and emitting one delta. This is intentional parity behaviour; real
  per-token streaming via OpenCode's `/event` endpoint is a future opt-in mode.
- **Fire-and-forget session delete.** The `SessionCleanupService` exists as a
  backstop; do not synchronise on the per-request delete.
- **`network_mode: host` in docker-compose.yml.** This is deliberate. OpenCode
  is typically bound to the host's loopback (`127.0.0.1:4096`); reaching it
  from a bridged container via `host.docker.internal` / `host-gateway` fails
  with `Connection refused` because packets arrive on the host's external
  interface, not its loopback. Sharing the host network namespace is the
  cleanest fix for a dev-only sidecar. Don't switch to bridge networking
  without also documenting the OpenCode `--host 0.0.0.0` rebind requirement.

## Logging

- Every `/v1/chat/completions` call emits a `chat request` line on entry and a
  `chat completion ok` line on success, both at `Information` and both carrying
  `ctx.TraceIdentifier` as the correlating `{RequestId}`. Keep that pair intact
  — it's the only per-request narrative, since `Microsoft.AspNetCore` is turned
  down to `Warning`.
- Prompt and completion **content** is gated behind `OPENCODE_OAI_LOG_PROMPTS`
  and always goes through `Logging/LogPreview.cs`, which collapses to one line,
  caps length, and elides images. Never log raw message content directly —
  base64 data URIs will flood the journal.
- Client disconnects log at `Debug`, not `Warning`. Editors cancel inline
  completions constantly; that is not an error condition.

## Env vars

Two prefixes, hard rule — no legacy aliases, no fallbacks:

- `OPENCODE_OAI_*` — bridge-side settings.
- `OPENCODE_*`     — upstream connection settings (talking to the OpenCode server).

All env → config mapping lives in `Configuration/EnvConfiguration.cs`. Add new
vars there and mirror them in the README table.

For local `dotnet run`, `src/OpencodeOai/appsettings.Development.json` is
git-ignored; copy from the `.example` sibling and edit. Env vars win over
`appsettings.Development.json`.
