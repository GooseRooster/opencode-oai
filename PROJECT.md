# PROJECT.md

Detailed design notes for `opencode-oai`. This is the "why" companion to
[AGENTS.md](./AGENTS.md), which is the "how".

## Goal

Expose a **minimal**, OpenAI-compatible HTTP API in front of a locally running
[OpenCode](https://opencode.ai) server, so that editor plugins that speak the
OpenAI protocol (inline completion, ghost-text, lightweight chat panels) can
transparently route through OpenCode's model routing without any plugin-side
awareness of OpenCode itself.

## Scope

### In scope

- `GET  /health` — bridge + upstream reachability.
- `GET  /openapi.json` — hand-written OpenAPI 3.1 spec for REST clients.
- `GET  /v1/models` — enumerate models from OpenCode's connected providers,
  with a last-known-good cache and a static fallback list.
- `POST /v1/chat/completions` — buffered non-streaming and buffered-parity SSE
  streaming; supports `Idempotency-Key`; supports multimodal image content
  (data URI + remote URL).
- API key bearer auth (`OPENCODE_PROXY_API_KEY`); disabled when unset.

### Out of scope

- **Tool / function calling.** `tools`, `tool_choice`, `tool_calls`, and
  `role: "tool"` are silently dropped with an info log. This is deliberate:
  inline completion clients don't send them, and supporting them properly
  would drag in workspace / permission concerns the bridge is trying to
  avoid. If you need tool calling, drive OpenCode directly.
- **Workspace-scoped filesystem operations.** Sessions are created without a
  `directory` field. The bridge has no opinion about the client's working
  directory. This sidesteps the classic host↔container path-resolution
  problem where the editor lives in a devcontainer at `/workspaces/foo` but
  OpenCode runs on the host and knows the same directory as
  `/home/you/repos/foo`.
- **Multi-turn conversation memory in the bridge.** Every request creates a
  fresh OpenCode session, uses it once, and best-effort deletes it. There is
  no `x-conversation-id` mapping. If your client wants persistent context it
  should include it in `messages` itself, which is the OpenAI convention
  anyway.
- **Real per-token streaming.** The buffered parity mode matches the npm
  bridge behaviour and is guaranteed to work with all OpenAI SSE clients.
  Real streaming via OpenCode's `/event` SSE endpoint is planned but not
  implemented — it will be a `STREAMING_MODE=events` opt-in.

## Architecture

```
┌───────────┐   OpenAI    ┌─────────────────┐   OpenCode REST   ┌──────────────┐
│  Editor   │ ──HTTP─────▶│  opencode-oai   │ ──HTTP──────────▶ │  OpenCode    │
│  plugin   │ ◀──SSE──────│  (this repo)    │ ◀──JSON──────────  │  server      │
└───────────┘             └─────────────────┘                   └──────────────┘
                              ▲       ▲
                              │       └── SessionCleanupService (backstop)
                              └────────── MemoryIdempotencyStore
```

### Composition

- `Program.cs` is a slim composition root: env config, JSON source-gen,
  options binding, auth scheme, endpoint registry, hosted services.
- `Endpoints/EndpointRegistry.cs` holds an explicit `IEndpoint[]` — no
  reflection scan, AOT-safe.
- `Bridge/ChatCompletionService.cs` is the translation layer: OpenAI request
  → `PartsBuilder` → `IOpenCodeClient.SendMessageAsync` → OpenAI response.
- `OpenCode/OpenCodeClient.cs` is a hand-written typed `HttpClient` covering
  only the six upstream endpoints the bridge consumes.

### Resilience

`Microsoft.Extensions.Http.Resilience` `AddResilienceHandler` on the upstream
client:

- Retry `RETRY_COUNT` times with exponential backoff + jitter, base
  `RETRY_DELAY_MS`.
- Retry only on `HttpRequestException`, `TimeoutRejectedException`, and 5xx.
- Total timeout via a Polly timeout strategy set to `TIMEOUT_MS`.
- `HttpContext.RequestAborted` propagates through to upstream `HttpClient`
  calls so client disconnects don't leak upstream work.

### Idempotency

`Idempotency-Key` header (optional). Keyed by `(Authorization, key)`. In-flight
same-key requests await the shared `Task<ChatCompletionResult>` instead of
duplicating upstream calls. Faulted tasks are evicted so subsequent callers
retry cleanly. 24h TTL by default.

### Session lifecycle

1. `POST /session` with `title = "bridge-<reqId>"`, no `directory`.
2. `POST /session/{id}/message`.
3. Fire-and-forget `DELETE /session/{id}` on the returning task.
4. `SessionCleanupService` runs every `CLEANUP_INTERVAL_MS`, listing all
   sessions and reaping any with a `bridge-` title prefix older than
   `SESSION_TTL_HOURS`. This is a backstop for cases where the process is
   killed mid-request or the delete call itself failed.

### Auth

`ApiKeyAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>`.
Reads `Authorization: Bearer <key>` (or the raw header) and compares against
`OPENCODE_PROXY_API_KEY` via `CryptographicOperations.FixedTimeEquals`. If
the env var is empty, all requests succeed as `anonymous` — parity with the
npm bridge, but a WARN is logged at startup.

Auth failures are rewritten to an OpenAI-shaped
`{ "error": { "message": "Unauthorized", "type": "auth_error" } }` body by a
tiny middleware.

### Logging

- `Microsoft.Extensions.Logging` + `AddJsonConsole` for stdout, UTC ISO-8601
  timestamps.
- `DevContainerFileLoggerProvider` registered only when `DEVCONTAINER=true`,
  writing JSON lines to `/tmp/console-dev.log` via a bounded
  `Channel<string>` with drop-oldest semantics — no reflection, AOT-clean.

### Native AOT

- `PublishAot=true`, `IsAotCompatible=true`, `TreatWarningsAsErrors=true`.
- All JSON serialisation goes through hand-declared `JsonSerializerContext`s
  (`OpenCodeJsonContext`, `OpenaiJsonContext`, `HealthJsonContext`,
  `AuthErrorJsonContext`).
- `Program.cs`'s single call to `Configuration.GetSection().Get<BridgeOptions>()`
  for early port binding is the only reflection-adjacent hop; it's a
  well-known trim-safe shape and works because `BridgeOptions` has a public
  parameterless ctor and public settable properties.
- Native binary produced inside the Dockerfile's `sdk:10.0` build stage —
  host-side publish tends to fail on missing zlib / brotli dev libs; see
  AGENTS.md.

## Env vars

Names match the npm bridge (`opencode-bridge`) exactly for drop-in
replacement. Full list in the README. `EnvConfiguration.cs` is the single
place that maps these flat names onto the `Bridge` / `OpenCode` config
sections consumed by the options pattern.

## Testing

- 25+ tests, all offline. No live OpenCode required.
- `PartsBuilderTests` — verifies the OpenAI → OpenCode part shape,
  including the deliberate tool-field drop.
- `IdempotencyStoreTests` — cache hit, concurrent coalescing, fault
  eviction.
- `ChatCompletionServiceTests` — mocked `IOpenCodeClient`, verifies
  provider/model split, usage totals, and fire-and-forget delete on both
  success and failure paths.
- `ApiKeyAuthTests` — missing / invalid / valid header; empty env disables.
- `EndpointsIntegrationTests` — `WebApplicationFactory<Program>` with a
  stubbed `IOpenCodeClient`, covering `/health`, `/openapi.json`,
  `/v1/models` (live + fallback), non-streaming, streaming, and validation.

Run everything via `devcontainer exec --workspace-folder . dotnet test`.

## Roadmap (probably)

- Real streaming behind `STREAMING_MODE=events` using OpenCode's `/event`
  SSE. Requires per-session event filtering and translating
  `message.part.updated` frames into OpenAI `chat.completion.chunk` deltas.
- Metrics endpoint (`/metrics` or OpenTelemetry export) if operator demand
  materialises.
- Model list live-refresh with background polling instead of on-demand.
