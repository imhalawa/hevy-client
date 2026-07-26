# hevy-client

`hevy-client` is a clean-room, local-first MCP server for the official [Hevy API](https://api.hevyapp.com/docs/). It gives AI agents complete typed access to the checked-in API snapshot plus bounded search and deterministic training-analysis tools. It contains no model, telemetry, credential store, or hosted service.

The default setup is one short-lived Docker container per MCP client over stdio. Nothing listens on a network port. An optional authenticated Streamable HTTP mode is available for a deliberately self-hosted, single-tenant deployment.

## What it exposes

- All 22 operations in the pinned Hevy OpenAPI snapshot as low-level tools: 14 reads and 8 writes.
- Routine and exercise-template search.
- Bounded workout evidence, training summaries, and exercise-history summaries.
- Two prompts for evidence-cited training analysis and routine-to-completed-workout preparation.
- An allowlist-only `get_diagnostics` tool.

Calculations are deterministic and include supporting identifiers and timestamps. The connected agent may interpret the evidence, but this server does not generate coaching or make model calls.

## Prerequisites

- Docker with Linux-container support.
- A Hevy API key. `HEVY_API_KEY` is the only accepted credential source.
- An MCP client that supports stdio, such as Codex, Claude Desktop, Cursor, VS Code, or Gemini CLI.

Build the local image from a reviewed checkout:

```sh
docker build --pull --tag hevy-client:local .
```

Load the key into the environment using your shell's secret-manager integration. On a private interactive Bash session, this avoids placing the value in shell history:

```sh
read -r -s -p "Hevy API key: " HEVY_API_KEY && export HEVY_API_KEY && printf '\n'
```

Do not put the key in a Dockerfile, image, MCP JSON/TOML, command argument, URL, source file, or committed `.env` file. Every Docker example below uses `-e HEVY_API_KEY` without a value so Docker copies the existing host variable into the container. The MCP client process must inherit that variable; restart a desktop client after setting it.

## Recommended local stdio setup

The common command is:

```sh
docker run --rm -i --read-only --tmpfs /tmp:rw,noexec,nosuid,size=16m -e HEVY_API_KEY hevy-client:local
```

`-i` is required because MCP uses stdin. There is intentionally no `-p` option. stdout is reserved for MCP; diagnostics, when enabled, use stderr.

The configurations below all run that same command. JSON snippets are complete documents; merge the shown server entry if the file already contains other settings.

### Codex

The current Codex CLI can add the server without recording the key:

```sh
codex mcp add hevy -- docker run --rm -i --read-only --tmpfs /tmp:rw,noexec,nosuid,size=16m -e HEVY_API_KEY hevy-client:local
codex mcp get hevy
```

Codex CLI, the Codex IDE extension, and the ChatGPT desktop app share `~/.codex/config.toml`. See the official [Codex MCP documentation](https://developers.openai.com/codex/mcp).

### Claude Desktop

Open Claude Desktop's developer settings and edit its MCP configuration:

```json
{
  "mcpServers": {
    "hevy": {
      "command": "docker",
      "args": [
        "run",
        "--rm",
        "-i",
        "--read-only",
        "--tmpfs",
        "/tmp:rw,noexec,nosuid,size=16m",
        "-e",
        "HEVY_API_KEY",
        "hevy-client:local"
      ]
    }
  }
}
```

Restart Claude Desktop after saving. Claude Code accepts the same `mcpServers` entry in `.mcp.json`; its verified CLI equivalent is `claude mcp add --transport stdio hevy -- docker run ...`. See Anthropic's [local MCP server guide](https://support.claude.com/en/articles/10949351-getting-started-with-local-mcp-servers-on-claude-desktop) and [Claude Code MCP reference](https://code.claude.com/docs/en/mcp).

### Cursor

Add this to the user MCP settings or `.cursor/mcp.json`, then enable the server in Cursor settings:

```json
{
  "mcpServers": {
    "hevy": {
      "command": "docker",
      "args": [
        "run",
        "--rm",
        "-i",
        "--read-only",
        "--tmpfs",
        "/tmp:rw,noexec,nosuid,size=16m",
        "-e",
        "HEVY_API_KEY",
        "hevy-client:local"
      ]
    }
  }
}
```

See the official [Cursor MCP documentation](https://cursor.com/docs/context/mcp).

### Visual Studio Code

Use the `MCP: Open User Configuration` command, or create `.vscode/mcp.json` for a trusted workspace:

```json
{
  "servers": {
    "hevy": {
      "type": "stdio",
      "command": "docker",
      "args": [
        "run",
        "--rm",
        "-i",
        "--read-only",
        "--tmpfs",
        "/tmp:rw,noexec,nosuid,size=16m",
        "-e",
        "HEVY_API_KEY",
        "hevy-client:local"
      ]
    }
  }
}
```

Start it from the MCP server view and approve only the tools you intend to use. See the official [VS Code MCP configuration reference](https://code.visualstudio.com/docs/agents/reference/mcp-configuration).

### Gemini CLI

Add this entry to the user `~/.gemini/settings.json` or project `.gemini/settings.json`:

```json
{
  "mcpServers": {
    "hevy": {
      "command": "docker",
      "args": [
        "run",
        "--rm",
        "-i",
        "--read-only",
        "--tmpfs",
        "/tmp:rw,noexec,nosuid,size=16m",
        "-e",
        "HEVY_API_KEY",
        "hevy-client:local"
      ],
      "trust": false
    }
  }
}
```

Keep `trust` false so Gemini asks before tool calls. Check discovery with `/mcp`. See the official [Gemini CLI MCP documentation](https://geminicli.com/docs/tools/mcp-server/).

### Other stdio clients

Configure executable `docker` with these arguments, in this exact order:

```text
run
--rm
-i
--read-only
--tmpfs
/tmp:rw,noexec,nosuid,size=16m
-e
HEVY_API_KEY
hevy-client:local
```

The client must send newline-delimited MCP JSON-RPC on stdin and keep stdin attached for the life of the server.

## Writes, read-only mode, and dry runs

Writes are enabled by default. MCP clients should present their normal approval UI for mutation tools.

To omit every mutation tool at discovery time, add these two Docker arguments before the image name:

```text
-e
HEVY_READ_ONLY=true
```

Every mutation accepts `dry_run: true`. A dry run validates and returns the normalized outbound payload and warnings without contacting Hevy. Replacement tools normally require the current object's `updated_at` as `expected_updated_at`; `force: true` explicitly bypasses that guard.

Hevy body measurements do not expose `updated_at`. `update_body_measurement` therefore cannot promise optimistic concurrency and requires `force: true` before an actual request is sent. Read the current measurement immediately before deciding to replace it. No delete tools exist because the official API snapshot exposes no delete operations.

## Optional authenticated HTTP self-hosting

HTTP mode is for one Hevy account per container. It is not a multi-user credential service. Use a separate high-entropy `MCP_AUTH_TOKEN`, keep the backend loopback-only, and terminate TLS at a reverse proxy.

Generate or retrieve the bearer token through a secret manager and export it without printing it. Token syntax is RFC token68; a base64 value is accepted. It must differ from `HEVY_API_KEY`.

```sh
MCP_AUTH_TOKEN="$(openssl rand -base64 32 | tr -d '\n')" && export MCP_AUTH_TOKEN

docker run --rm --name hevy-client-http \
  --read-only \
  --tmpfs /tmp:rw,noexec,nosuid,size=16m \
  -e HEVY_API_KEY \
  -e MCP_AUTH_TOKEN \
  -e HEVY_MCP_TRANSPORT=http \
  -e ASPNETCORE_URLS=http://0.0.0.0:8080 \
  -e AllowedHosts=hevy.example.net \
  -p 127.0.0.1:8080:8080 \
  hevy-client:local
```

A minimal Caddy site on the same host is:

```caddyfile
hevy.example.net {
  reverse_proxy 127.0.0.1:8080
}
```

Caddy obtains and terminates TLS. The proxy must preserve the original `Host`. Configure `AllowedHosts` as an explicit semicolon-separated list of trusted public authorities; wildcards are rejected. If a client supplies `Origin`, the server accepts it only when its authority exactly matches `Host`; plain-HTTP origins are accepted only for loopback. Do not publish port 8080 on `0.0.0.0`, bypass TLS, or expose this single-tenant service as a shared public endpoint.

The MCP URL is `https://hevy.example.net/mcp`. Clients must send `Authorization: Bearer` using the separate token. `/healthz` is unauthenticated and returns only an empty `200`; do not treat it as proof that Hevy credentials work.

Codex can source the HTTP token from the environment:

```sh
codex mcp add hevy-http --url https://hevy.example.net/mcp --bearer-token-env-var MCP_AUTH_TOKEN
```

To rotate the bearer token, update the secret in every authorized client, stop the container, and recreate it with the new value. The old token stops working when the old process exits. Rotate the Hevy key separately if it may have been exposed.

## Configuration reference

| Variable | Required | Default | Accepted values and behavior |
|---|---:|---|---|
| `HEVY_API_KEY` | Always | None | Nonblank Hevy API key. Never accepted as a tool input or command-line option. |
| `HEVY_MCP_TRANSPORT` | No | `stdio` | Exactly `stdio` or `http`. |
| `MCP_AUTH_TOKEN` | HTTP only | None | Nonblank token68 bearer token, distinct from the Hevy key. Ignored in stdio mode. |
| `HEVY_READ_ONLY` | No | `false` | Exactly `true` or `false`; `true` omits all mutation tools. |
| `HEVY_LOG_LEVEL` | No | `None` | Exactly `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, or `None`. |
| `AllowedHosts` | HTTP only | `localhost;127.0.0.1;[::1]` | Explicit semicolon-separated trusted hosts. Wildcards and `+` are rejected. |
| `ASPNETCORE_URLS` | HTTP only | Image port 8080 | ASP.NET listen URL. Keep the Docker publication loopback-only behind TLS. |

Unknown, blank, or differently cased custom values fail startup. HTTP startup also fails if its two tokens are equal.

## Privacy and diagnostics

There is no telemetry, crash upload, update checker, analytics endpoint, or persistent fitness-data cache. Runtime traffic goes only to the fixed `https://api.hevyapp.com` origin. Process-local routine and exercise-template caches expire after 15 minutes and disappear on restart; cache keys never contain credentials.

Diagnostics are off by default. When `HEVY_LOG_LEVEL` is enabled, allowlisted JSON records go to stderr and contain only server/runtime category data, operation category, bucketed duration, status, safe upstream request identifiers, local correlation identifiers, and exception category. They never contain headers, URLs with queries, request or response bodies, workout text, activity timestamps, or measurements. Sink failures are contained and cannot change a tool result.

Call `get_diagnostics` for a safe snapshot containing only server version, runtime version, transport, read-only state, diagnostics state, and health. Users choose whether to copy that output into an issue; the server uploads nothing.

## Bounds and current limitations

- Low-level Hevy pages preserve explicit page semantics and accept at most 10 items per page.
- Composite calls default to 100 returned items and cap each invocation at 1,000 scanned or returned items. Continue with the exact returned continuation inputs when `truncated` is true.
- Training windows default to 4 UTC weeks and cap at 52 weeks. Partial chunks label whether metrics cover the complete period or only that chunk.
- Routine and exercise-template catalogs are each capped at 1,000 cached items.
- Hevy's exercise-history endpoint is unpaginated. The response is streamed with independent 1,000-item and 16 MiB ceilings. Results state whether truncation is continuable or terminal; the server never silently claims completeness beyond those caps.
- Body-measurement replacement is force-only because the upstream response has no `updated_at` field.
- This is single-tenant and has no OAuth server, browser credential capture, tunnels, multi-user storage, embedded LLM, subjective coaching, MCP bulk resources, or invented delete operations.

## Version and image pinning

Release users should choose an immutable semantic version or the exact image digest from the release, not an automatically moving tag. Replace `hevy-client:local` in client configurations with that reviewed `registry/name@sha256:digest` reference.

Both Docker base images are pinned by multi-architecture manifest digest. To update them deliberately:

```sh
docker buildx imagetools inspect mcr.microsoft.com/dotnet/sdk:10.0-noble
docker buildx imagetools inspect mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled
```

Review Microsoft's [.NET container image documentation](https://learn.microsoft.com/dotnet/core/docker/container-images), replace both digests in `Dockerfile`, rebuild with `--pull`, and run the full test and container-smoke suites. A digest update is a reviewed dependency change, not an automatic runtime action.

Local builds label version, revision, and source as development values. Public distributors must set `VERSION`, `REVISION`, and `SOURCE_URL` build arguments to the immutable release version, full source commit, and canonical repository URL; these values are metadata and must never contain secrets.

## Development

The repository targets .NET 10 and uses locked NuGet restores:

```sh
dotnet restore --locked-mode
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
```

See [CONTRIBUTING.md](CONTRIBUTING.md) for clean-room and test requirements, [SECURITY.md](SECURITY.md) for private vulnerability reporting, and [LICENSE](LICENSE) for the MIT license.
