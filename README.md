# opencode-oai

Minimal, container-native .NET 10 bridge that exposes an OpenAI-compatible API
(`/v1/models`, `/v1/chat/completions`) and proxies each call to a locally
running [OpenCode](https://opencode.ai) server.

Designed for **inline completion / lightweight chat** in editor plugins. Tool
calling and workspace-scoped filesystem operations are intentionally out of
scope. Keeping the surface small avoids host↔container path resolution
headaches when the OpenCode server runs on the host and the editor runs in a
devcontainer.

## Attribution

Inspired by [opencode-bridge](https://github.com/crazyboy24/opencode-bridge), rewritten in .NET

This project is AI-assisted. All contributions are owned by maintainers.

## Features

- OpenAI-shaped `/v1/chat/completions` (streaming + non-streaming) and `/v1/models`.
- API key bearer auth, gated on `OPENCODE_PROXY_API_KEY`.
- `Idempotency-Key` support with in-flight coalescing.
- Polly-backed resilience (retry + timeout + circuit breaker) on the upstream client.
- Fire-and-forget session delete after each request plus a periodic reaper backstop.
- Structured JSON logs to stdout with per-request lines; opt-in prompt/completion
  previews. Optional `/tmp/console-dev.log` mirror when `DEVCONTAINER=true`.
- Podman Quadlet unit for running the bridge as a systemd service.
- Native AOT publish → small self-contained container image.

## Quick start

### docker compose

```sh
docker compose up --build
```

The bridge listens on `http://localhost:5000` and reaches OpenCode at
`http://localhost:4096`. The compose file uses `network_mode: host` so the
container shares the host's network namespace — that's the reliable way to
reach an OpenCode server that's bound to the host's loopback (`127.0.0.1`).
Works with both Docker and Podman on Linux.

If your OpenCode server is bound to all interfaces (`0.0.0.0`) you can drop
`network_mode: host`, re-add a `ports:` mapping, and point `OPENCODE_URL` at
`http://host.docker.internal:4096` with an `extra_hosts` gateway alias.

### docker run

```sh
docker build -t opencode-oai .
docker run --rm --network=host \
  -e OPENCODE_URL=http://localhost:4096 \
  -e OPENCODE_OAI_API_KEY=change-me \
  opencode-oai
```

### podman quadlet (systemd service)

The intended way to leave the bridge running on a workstation. Quadlet turns a
`.container` file into a systemd service, so the bridge starts with your
session, restarts on failure, and logs to the journal — and your API keys live
in a `600` env file outside the repo rather than in a compose file.

Templates: [`deploy/quadlet/opencode-oai.container`](deploy/quadlet/opencode-oai.container)
and [`deploy/quadlet/opencode-oai.env.example`](deploy/quadlet/opencode-oai.env.example).
Requires podman ≥ 4.6.

```sh
# 1. Build the image locally (the unit references localhost/opencode-oai:latest)
podman build -t localhost/opencode-oai:latest .

# 2. Install the unit and its environment file
install -Dm644 deploy/quadlet/opencode-oai.container \
  ~/.config/containers/systemd/opencode-oai.container
install -Dm600 deploy/quadlet/opencode-oai.env.example \
  ~/.config/opencode-oai/opencode-oai.env

# 3. Set OPENCODE_OAI_API_KEY and anything else you need
$EDITOR ~/.config/opencode-oai/opencode-oai.env

# 4. Generate the service and start it
systemctl --user daemon-reload
systemctl --user start opencode-oai
```

```sh
systemctl --user status opencode-oai
journalctl --user -u opencode-oai -f      # structured JSON logs
systemctl --user restart opencode-oai     # after an image rebuild
```

There is no `enable` step — Quadlet-generated units are transient, and the
unit's `WantedBy=default.target` makes it start at login. To keep it running
while you're logged out: `loginctl enable-linger $USER`.

For a system-wide install, drop the same file in `/etc/containers/systemd/`,
put the env file at `/root/.config/opencode-oai/opencode-oai.env` (`%h` resolves
to `/root`), and use `systemctl` without `--user`.

The unit uses `Network=host` for the same reason compose does, and runs the
container read-only, with `no-new-privileges` and all capabilities dropped.

### dotnet run (development)

```sh
cp src/OpencodeOai/appsettings.Development.json.example \
   src/OpencodeOai/appsettings.Development.json
# tweak values as needed; the file is git-ignored
dotnet run --project src/OpencodeOai
```

Env vars still win over `appsettings.Development.json` when both are set.

## Configuration

All configuration is read from environment variables.

Naming scheme:

- `OPENCODE_OAI_*` — bridge-side settings (this service's own config)
- `OPENCODE_*`     — upstream connection settings (talking to the OpenCode server)

### Bridge settings

| Env                                | Default          | Purpose                                                     |
| ---------------------------------- | ---------------- | ----------------------------------------------------------- |
| `OPENCODE_OAI_PORT`                | `5000`           | Kestrel bind port                                           |
| `OPENCODE_OAI_API_KEY`             | _empty_          | Bearer key required from clients (auth disabled when empty) |
| `OPENCODE_OAI_DEFAULT_PROVIDER`    | `github-copilot` | Default provider when the client sends a bare model         |
| `OPENCODE_OAI_DEFAULT_MODEL`       | `gpt-4o`         | Default model                                               |
| `OPENCODE_OAI_HEARTBEAT_MS`        | `15000`          | SSE keepalive interval                                      |
| `OPENCODE_OAI_SESSION_TTL_HOURS`   | `2`              | Age at which orphaned sessions are reaped                   |
| `OPENCODE_OAI_CLEANUP_INTERVAL_MS` | `3600000`        | Reaper interval                                             |
| `OPENCODE_OAI_IDEMPOTENCY_TTL_HOURS` | `24`           | Idempotency cache TTL                                       |
| `OPENCODE_OAI_LOG_LEVEL`           | `Information`    | Min log level                                               |
| `OPENCODE_OAI_LOG_PROMPTS`         | `false`          | If `true`, log prompt + completion content, not just metadata |
| `OPENCODE_OAI_LOG_PREVIEW_CHARS`   | `500`            | Per-preview character cap (`0` = unbounded)                 |
| `OPENCODE_OAI_DEVCONTAINER`        | _unset_          | If `true`, also write JSON logs to `/tmp/console-dev.log`   |

### Upstream OpenCode settings

| Env                       | Default                  | Purpose                                       |
| ------------------------- | ------------------------ | --------------------------------------------- |
| `OPENCODE_URL`            | `http://localhost:4096`  | Upstream OpenCode server                      |
| `OPENCODE_USERNAME`       | `opencode`               | Basic-auth username                           |
| `OPENCODE_PASSWORD`       | _empty_                  | Basic-auth password (auth skipped when empty) |
| `OPENCODE_TIMEOUT_MS`     | `600000`                 | Upstream request timeout                      |
| `OPENCODE_RETRY_COUNT`    | `2`                      | Polly retry count                             |
| `OPENCODE_RETRY_DELAY_MS` | `2000`                   | Polly base delay                              |

## Logging

Logs are structured JSON on stdout — the journal under systemd, `podman logs`
otherwise. ASP.NET Core's own request logging is turned down to `Warning`, so
the per-request narrative comes from the bridge itself. Every completion emits
two `Information` lines, correlated by the ASP.NET request id (`{RequestId}`):

```text
chat request 0HNNNFS2VNUMO:00000001 model=github-copilot/gpt-4o messages=2 stream=False
chat completion ok 0HNNNFS2VNUMO:00000001 model=github-copilot/gpt-4o stream=False in 8807ms tokens=in:72 out:19 total:8426 chars=33
```

(Message text only — each line is wrapped in the JSON envelope with timestamp,
level, category and the named fields broken out for querying.)

`total` is OpenCode's own figure and counts cache and reasoning tokens, so it
will not generally equal `in + out`.

Client disconnects — routine when an editor cancels an inline completion — log
at `Debug`, so they stay out of the way at the default level. Upstream and
bridge failures log at `Error` with the same request id.

Set `OPENCODE_OAI_LOG_PROMPTS=true` to also log content:

```text
chat prompt 0HNNNFS2VNUMO:00000001 system: You are a code completion engine. Reply with code only. | user: Complete: for (var i = 0; i <
chat output 0HNNNFS2VNUMO:00000001 `for (var i = 0; i < n; i++) { }`
```

Previews are collapsed onto one line and capped at `OPENCODE_OAI_LOG_PREVIEW_CHARS`
(default 500), with the withheld character count appended. Image parts are
elided as `<image>` rather than dumping base64 data URIs. Reasoning output, when
the model produces any, gets its own `chat reasoning` line.

This is off by default on purpose: prompts carry whatever source code the editor
decided to send, and inline completion sends a lot of it. Turn it on to debug a
plugin, not as a standing default.

## Example: non-streaming

```sh
curl http://localhost:5000/v1/chat/completions \
  -H "Authorization: Bearer $OPENCODE_OAI_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "github-copilot/gpt-4o",
    "messages": [
      {"role": "user", "content": "Write a haiku about static typing."}
    ]
  }'
```

## Example: streaming (SSE)

```sh
curl -N http://localhost:5000/v1/chat/completions \
  -H "Authorization: Bearer $OPENCODE_OAI_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "github-copilot/gpt-4o",
    "stream": true,
    "messages": [
      {"role": "user", "content": "Complete: for (var i = 0; i <"}
    ]
  }'
```

> Streaming is currently buffered-parity: the bridge awaits the full upstream
> response, then emits it as a single delta plus a terminating chunk. This
> matches the behaviour of the original npm bridge and works with all OpenAI-
> compatible SSE clients. Real per-token streaming via OpenCode's `/event`
> endpoint is planned as an opt-in mode.

## Example: editor plugin config (Continue-style)

```json
{
  "models": [
    {
      "provider": "openai",
      "apiBase": "http://localhost:5000/v1",
      "apiKey": "change-me",
      "model": "github-copilot/gpt-4o",
      "title": "OpenCode via opencode-oai"
    }
  ]
}
```

## Scope

**Supported.** Text prompts, multi-modal images (`image_url` including data-URIs
and remote URLs), streaming and non-streaming responses, provider/model routing
via `provider/model` prefix, `Idempotency-Key` retries, reasoning-model output.

**Reasoning / thinking.** OpenCode has no per-request reasoning-effort knob;
reasoning is a per-model capability. To enable it, select a reasoning-capable
model in the `model` field (e.g. `github-copilot/gpt-5-thinking`,
`openai/o1`, `anthropic/claude-3.7-sonnet-thinking`). The bridge extracts
`type: "reasoning"` parts from OpenCode's response and surfaces them as
`message.reasoning_content` (buffered) or `delta.reasoning_content` (streaming),
matching the DeepSeek / OpenRouter / LiteLLM convention that most inline-
completion clients (Continue, Cursor, etc.) already understand. The
`reasoning_effort` request field is accepted for client compatibility but
silently dropped with an info log.

**Not supported.** Tool / function calling — `tools`, `tool_choice`, `tool_calls`,
and `role: "tool"` messages are silently dropped with an info log. Workspace-
scoped filesystem operations (no `directory` on sessions) — you don't need to
worry about host↔container path mapping. If you need any of these, drive OpenCode
directly.

## Development

```sh
dotnet build
dotnet test
dotnet run --project src/OpencodeOai
```

Verify AOT publish locally (requires zlib/brotli dev libs, or run inside the
Docker build):

```sh
dotnet publish src/OpencodeOai -c Release -r linux-x64
```

## Endpoints

| Method | Path                    | Auth | Notes                              |
| ------ | ----------------------- | ---- | ---------------------------------- |
| GET    | `/health`               | none | Bridge + upstream reachability     |
| GET    | `/openapi.json`         | none | OpenAPI 3.1 spec for REST clients  |
| GET    | `/v1/models`            | key  | Live provider list w/ fallback     |
| POST   | `/v1/chat/completions`  | key  | Streaming and non-streaming        |

A `.http` file with ready-to-fire requests lives at
[`src/OpencodeOai/OpencodeOai.http`](src/OpencodeOai/OpencodeOai.http).

## Contributing
Contributions welcome. Feel free to make pull requests with improvements. Please try and keep pull requests focused.

AI assisted PRs are of course welcome but you need to be able to explain and defend the code.

## License
MIT
