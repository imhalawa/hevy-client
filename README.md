# hevy-client

`hevy-client` is a local MCP server for the [Hevy API](https://api.hevyapp.com/docs/), a workout-tracking API. It lets an AI client read and manage your Hevy data without sending it through a hosted intermediary.

## What it enables

- Typed access to all 22 operations in the pinned Hevy API snapshot: 14 reads and 8 writes.
- Routine and exercise-template search.
- Bounded workout evidence, exercise-history summaries, and deterministic training analysis.
- MCP prompts for evidence-cited training analysis and routine-to-workout preparation.

It does not run a model, provide coaching, store fitness data, or send telemetry.

## Quick start

You need Docker with Linux-container support, a Hevy API key, and an MCP client. The image contains no API key. `-e HEVY_API_KEY` passes your key to the container only when it starts.

Pull the released image by digest:

```sh
docker pull ghcr.io/imhalawa/hevy-client@sha256:f29625b6c0090af492e5115d186cb61583b5f903d79a5d6a73452e7c53188841
```

In a private Bash session, enter the key without putting it in shell history:

```sh
read -r -s -p "Hevy API key: " HEVY_API_KEY && export HEVY_API_KEY && printf '\n'
```

Run the server:

```sh
docker run --rm -i --read-only --tmpfs /tmp:rw,noexec,nosuid,size=16m \
  -e HEVY_API_KEY \
  ghcr.io/imhalawa/hevy-client@sha256:f29625b6c0090af492e5115d186cb61583b5f903d79a5d6a73452e7c53188841
```

The server will wait without output. That is expected: MCP uses JSON-RPC over standard input and output. Stdio mode publishes no network port.

### Windows PowerShell

With Docker Desktop running, this prompts for the key, starts the same hardened container, and removes the host environment variable afterward:

```powershell
docker pull ghcr.io/imhalawa/hevy-client@sha256:f29625b6c0090af492e5115d186cb61583b5f903d79a5d6a73452e7c53188841

$secureKey = Read-Host -Prompt 'Hevy API key' -AsSecureString
$keyPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureKey)

try {
  $env:HEVY_API_KEY = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($keyPointer)

  docker run --rm -i --read-only `
    --tmpfs /tmp:rw,noexec,nosuid,size=16m `
    -e HEVY_API_KEY `
    ghcr.io/imhalawa/hevy-client@sha256:f29625b6c0090af492e5115d186cb61583b5f903d79a5d6a73452e7c53188841
}
finally {
  [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($keyPointer)
  Remove-Item Env:HEVY_API_KEY -ErrorAction SilentlyContinue
}
```

## Connect an MCP client

For Codex CLI, run this from the shell that has `HEVY_API_KEY` set:

```sh
codex mcp add hevy -- docker run --rm -i --read-only --tmpfs /tmp:rw,noexec,nosuid,size=16m -e HEVY_API_KEY ghcr.io/imhalawa/hevy-client@sha256:f29625b6c0090af492e5115d186cb61583b5f903d79a5d6a73452e7c53188841
```

Other stdio MCP clients use the same Docker command. A graphical client must obtain the key before it starts the container; use the macOS/Linux [secret-store launcher](scripts/hevy-client-mcp) or the [Windows secure-prompt launcher](scripts/Start-HevyClient.ps1) instead of saving a key in the client configuration.

## Safe operation

- Never put a key in source code, an image layer, a command argument, a URL, or a committed environment file.
- Set `HEVY_READ_ONLY=true` to hide every mutation tool.
- Mutation tools accept `dry_run: true` to validate a request without contacting Hevy.
- This server is single-tenant. It has no OAuth server, multi-user storage, or public hosted service.

## Optional HTTP mode

HTTP mode is for one Hevy account behind your own TLS reverse proxy. It requires a distinct `MCP_AUTH_TOKEN`; keep the container loopback-bound and do not expose it as a shared public service.

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
  ghcr.io/imhalawa/hevy-client@sha256:f29625b6c0090af492e5115d186cb61583b5f903d79a5d6a73452e7c53188841
```

Terminate TLS at the proxy, preserve the original `Host`, and configure `AllowedHosts` with explicit public authorities. The MCP endpoint is `https://hevy.example.net/mcp`; clients authenticate with `Authorization: Bearer <MCP_AUTH_TOKEN>`. `/healthz` is unauthenticated and only confirms that the process is running.

## Configuration

| Variable | Required | Default | Behavior |
|---|---:|---|---|
| `HEVY_API_KEY` | Always | None | Nonblank Hevy API key. |
| `HEVY_MCP_TRANSPORT` | No | `stdio` | `stdio` or `http`. |
| `MCP_AUTH_TOKEN` | HTTP only | None | Nonblank token68 bearer token, distinct from the Hevy key. |
| `HEVY_READ_ONLY` | No | `false` | `true` hides mutation tools; otherwise `false`. |
| `HEVY_LOG_LEVEL` | No | `None` | `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, or `None`. |
| `AllowedHosts` | HTTP only | `localhost;127.0.0.1;[::1]` | Explicit semicolon-separated trusted hosts; wildcards are rejected. |
| `ASPNETCORE_URLS` | HTTP only | Image port 8080 | ASP.NET listen URL; publish only to loopback behind TLS. |

Unknown or malformed values fail startup. HTTP mode also fails if its bearer token equals the Hevy API key.

## Project

- [Contributing](CONTRIBUTING.md)
- [Security policy](SECURITY.md)
- [Release verification](docs/release-verification.md)
- [Release checklist](docs/release-checklist.md)
- [MIT License](LICENSE)
