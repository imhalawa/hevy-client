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

For a terminal client launched from the same private Bash session, this avoids placing the value in shell history:

```sh
read -r -s -p "Hevy API key: " HEVY_API_KEY && export HEVY_API_KEY && printf '\n'
```

Do not put the key in a Dockerfile, image, MCP JSON/TOML, command argument, URL, source file, or committed `.env` file. Every Docker example uses `-e HEVY_API_KEY` without a value so Docker copies the existing host variable into the container. Shell exports apply only to programs started from that shell; they are not a reliable way to provision an already-running graphical application.

## Recommended local stdio setup

The common command is:

```sh
docker run --rm -i --read-only --tmpfs /tmp:rw,noexec,nosuid,size=16m -e HEVY_API_KEY hevy-client:local
```

`-i` is required because MCP uses stdin. There is intentionally no `-p` option. stdout is reserved for MCP; diagnostics, when enabled, use stderr.

The configurations below all run that same command. JSON snippets are complete documents; merge the shown server entry if the file already contains other settings.

## Desktop clients without persisted API keys

Graphical clients need a launcher that obtains the key before the client or MCP process starts. This repository includes two launchers, and their container seams are tested with generated fake credentials and a real MCP handshake. The tests also check that the fake value is not written to launcher or temporary files.

### macOS Keychain

Copy `scripts/hevy-client-mcp` to a stable, user-owned absolute path such as `~/.local/bin/hevy-client-mcp`, then make it executable. In **Keychain Access**, create a **Generic Password** whose service/name is `hevy-client-api-key`, whose account is your current macOS username, and whose password is the Hevy API key. This avoids putting the key in a command argument or shell history.

The launcher retrieves the value from macOS Keychain for each MCP startup and passes only the environment-variable name to Docker. Point the desktop client's MCP `command` directly at the launcher's absolute path.

### Linux Secret Service

Install a Secret Service provider and the `secret-tool` client, then copy `scripts/hevy-client-mcp` to a stable, user-owned absolute path such as `~/.local/bin/hevy-client-mcp` and make it executable. Store the key without placing it in an argument or shell history:

```bash
read -r -s -p "Hevy API key: " hevy_key && printf '%s' "$hevy_key" | secret-tool store --label='hevy-client Hevy API key' service hevy-client credential api-key
unset hevy_key
printf '\n'
```

The graphical session's keyring must be unlocked. The launcher retrieves `service=hevy-client, credential=api-key` for each MCP startup, keeps it in process memory, and replaces itself with Docker. Point the desktop client's MCP `command` at the launcher's absolute path.

The launcher uses `hevy-client:local` by default. Set `HEVY_CLIENT_IMAGE` in the launcher's environment only when selecting a different reviewed image; it is not a credential.

### Windows secure-prompt launcher

Windows users can start a desktop client through `scripts/Start-HevyClient.ps1`. It securely prompts for the key, keeps the plaintext only in process memory and the launched process environment, and restores the previous environment after that client exits. The key is not stored in the MCP configuration or command history.

First review the script and, if Windows marked the downloaded file as blocked, run `Unblock-File .\scripts\Start-HevyClient.ps1`. Then start the client from a trusted PowerShell session, supplying its actual installed executable path:

```powershell
powershell -NoProfile -File .\scripts\Start-HevyClient.ps1 -ClientPath "$env:LOCALAPPDATA\Programs\Microsoft VS Code\Code.exe"
```

Fully exit every existing instance of the graphical client first; a single-instance application that reconnects to an older process will not receive the new environment. Use the installed executable path for Claude Desktop, Cursor, Codex, or another supported client in place of the VS Code example. Configure that client to run the common `docker run ... -e HEVY_API_KEY hevy-client:local` stdio command shown above. Because the launcher starts the process that hosts MCP, Docker inherits the prompted value without storing it.

### Codex

When Codex CLI is launched from the same terminal in which `HEVY_API_KEY` was exported, it can add the server without recording the key:

```sh
codex mcp add hevy -- docker run --rm -i --read-only --tmpfs /tmp:rw,noexec,nosuid,size=16m -e HEVY_API_KEY hevy-client:local
codex mcp get hevy
```

On macOS or Linux, graphical Codex clients can instead point their shared `~/.codex/config.toml` entry at the secret-backed launcher:

```toml
[mcp_servers.hevy]
command = "/absolute/path/to/hevy-client-mcp"
```

On Windows, retain the Docker MCP command and start the graphical client through `scripts/Start-HevyClient.ps1`. See the official [Codex MCP documentation](https://developers.openai.com/codex/mcp).

### Claude Desktop

On macOS or Linux, open Claude Desktop's developer settings and point its MCP configuration at the installed secret-backed launcher:

```json
{
  "mcpServers": {
    "hevy": {
      "command": "/absolute/path/to/hevy-client-mcp"
    }
  }
}
```

Start Claude Desktop normally after saving. On Windows, keep the Docker command and start Claude Desktop through the PowerShell launcher. Claude Code can use the same launcher entry in `.mcp.json`; a terminal session with an exported key can instead use `claude mcp add --transport stdio hevy -- docker run ...`. See Anthropic's [local MCP server guide](https://support.claude.com/en/articles/10949351-getting-started-with-local-mcp-servers-on-claude-desktop) and [Claude Code MCP reference](https://code.claude.com/docs/en/mcp).

### Cursor

On macOS or Linux, add this to the user MCP settings or `.cursor/mcp.json`, then enable the server in Cursor settings:

```json
{
  "mcpServers": {
    "hevy": {
      "command": "/absolute/path/to/hevy-client-mcp"
    }
  }
}
```

On Windows, keep the Docker command and start Cursor through the PowerShell launcher. See the official [Cursor MCP documentation](https://cursor.com/docs/context/mcp).

### Visual Studio Code

On macOS or Linux, use the `MCP: Open User Configuration` command, or create `.vscode/mcp.json` for a trusted workspace:

```json
{
  "servers": {
    "hevy": {
      "type": "stdio",
      "command": "/absolute/path/to/hevy-client-mcp"
    }
  }
}
```

Start it from the MCP server view and approve only the tools you intend to use. On Windows, keep the Docker command and start VS Code through the PowerShell launcher. See the official [VS Code MCP configuration reference](https://code.visualstudio.com/docs/agents/reference/mcp-configuration).

### Gemini CLI

On macOS or Linux, add this entry to the user `~/.gemini/settings.json` or project `.gemini/settings.json`:

```json
{
  "mcpServers": {
    "hevy": {
      "command": "/absolute/path/to/hevy-client-mcp",
      "trust": false
    }
  }
}
```

Keep `trust` false so Gemini asks before tool calls. Check discovery with `/mcp`. A terminal-only Gemini CLI can retain the direct Docker command when it inherits an exported key. On Windows, start Gemini's graphical host through the PowerShell launcher. See the official [Gemini CLI MCP documentation](https://geminicli.com/docs/tools/mcp-server/).

### Other stdio clients

Terminal clients that inherit an exported key can configure executable `docker` with these arguments, in this exact order:

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

The client must send newline-delimited MCP JSON-RPC on stdin and keep stdin attached for the life of the server. On macOS or Linux, a graphical client should use `/absolute/path/to/hevy-client-mcp` as its command instead. On Windows, use the same Docker arguments but start the client through `scripts/Start-HevyClient.ps1`.

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

Release users should prefer the exact image digest from the release. A semantic-version tag is a reviewed convenience reference, but GHCR does not enforce immutable tags; only the digest is content-addressed. Replace `hevy-client:local` in client configurations with the reviewed `registry/name@sha256:digest` reference.

Both Docker base images are pinned by multi-architecture manifest digest. To update them deliberately:

```sh
docker buildx imagetools inspect mcr.microsoft.com/dotnet/sdk:10.0-noble
docker buildx imagetools inspect mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled
```

Review Microsoft's [.NET container image documentation](https://learn.microsoft.com/dotnet/core/docker/container-images), replace both digests in `Dockerfile`, rebuild with `--pull`, and run the full test and container-smoke suites. A digest update is a reviewed dependency change, not an automatic runtime action.

Local builds label version, revision, and source as development values. Public distributors must set `VERSION`, `REVISION`, and `SOURCE_URL` build arguments to the immutable release version, full source commit, and canonical repository URL; these values are metadata and must never contain secrets.

## Verified releases

The release workflow accepts only an exact `vX.Y.Z` Git tag. Lightweight and annotated tags are both supported, but the checked-out commit, workflow source SHA, tag target, .NET assembly version, OCI revision, and OCI version must all agree. It publishes one `X.Y.Z` GHCR tag for `linux/amd64` and `linux/arm64`; it never publishes `latest`, major-only, or minor-only tags.

Public distribution remains fail-closed until the canonical repository and private security intake have been verified. Before the first tag:

1. Complete every blocking item in [the public distribution checklist](docs/release-checklist.md).
2. Create a protected GitHub Actions environment named `release`, require approval, and make this workflow its only package writer.
3. Set `HEVY_CANONICAL_REPOSITORY` to the exact `OWNER/REPOSITORY` name as a repository or `release`-environment variable.
4. Set `HEVY_PRIVATE_ADVISORY_VERIFIED=true` only after private vulnerability reporting is enabled and its link has been tested.

The workflow itself has only `contents:read`, `packages:write`, `id-token:write`, and `attestations:write`. Before registry authentication it independently repeats every non-live build, test, audit, and real-container gate, including two registry-free no-cache multi-architecture exports whose index and platform digests must match exactly. CI runs the same reproducibility gate. The release then performs the GHCR Registry v2 Bearer challenge and scoped token exchange, stages the multi-architecture result under its digest only, verifies both platform manifests and exact OCI labels, and exercises the staged amd64 assembly over MCP. The staged index excludes invocation-specific inline attestations and uses the source commit timestamp for reproducible image metadata. Exact-checksum-pinned Syft generates separate SPDX 2.3 documents for both platform digests; GitHub provenance and SBOM attestations are then created and verified, and the manifest digest is keylessly signed and verified with Cosign.

The final step repeats the authenticated tag lookup immediately before promotion. A tag already resolving to the verified digest is an idempotent success; a different digest fails. When the tag is absent, the Buildx tag creation is the workflow's last fallible command. The repository-wide concurrency group and protected `release` environment serialize this workflow, and maintainers must prevent every other workflow or credential from writing this package. This is race mitigation, not registry-enforced immutability: GHCR documents no atomic create-only or immutable-tag operation, so an independent package writer could still race or later move the tag. Consumers should pin `@sha256:DIGEST`.

The idempotent branch covers another writer selecting the same staged digest during the final race window and a rerun of the same source, version, and pinned toolchain. The separate provenance and SBOM attestations cannot perturb the staged image digest. If final promotion reports a transport failure, authenticate the tag lookup before deciding what to do: the original verified digest is already complete, a tag resolving to that digest is success, and an absent tag permits the serialized workflow to retry promotion. Only a genuinely different or unverifiable digest enters manual recovery and blocks release until its source, signature, and attestations have been investigated.

The workflow deliberately cannot create or modify a GitHub Release because it has read-only repository-content permission. Its SBOMs remain workflow artifacts for 90 days. A maintainer downloads them, attaches them to a draft GitHub Release, repeats the documented digest verification, and only then publishes that GitHub Release immutably.

After replacing the example owner, repository, version, and digest with the values from the successful release run, verify the image rather than trusting a tag alone:

```sh
cosign verify ghcr.io/OWNER/REPOSITORY@sha256:DIGEST \
  --certificate-identity https://github.com/OWNER/REPOSITORY/.github/workflows/release.yml@refs/tags/vX.Y.Z \
  --certificate-github-workflow-sha COMMIT_SHA \
  --certificate-oidc-issuer https://token.actions.githubusercontent.com

gh attestation verify oci://ghcr.io/OWNER/REPOSITORY@sha256:DIGEST \
  --repo OWNER/REPOSITORY \
  --signer-workflow OWNER/REPOSITORY/.github/workflows/release.yml
```

Every external GitHub Action is pinned by its complete commit SHA. Human-readable versions and reviewed source links live in [`.github/actions-lock.json`](.github/actions-lock.json); an action update must change the workflow pin and that lock document together. The actionlint and Syft archive checksums, Buildx version, and binfmt/BuildKit manifest digests are recorded separately in [`.github/tools-lock.json`](.github/tools-lock.json). Dependabot groups weekly minor and patch NuGet, Docker, and Actions updates. Major updates remain separate and require explicit maintainer review; MCP 2.x is ignored until a deliberate SDK migration updates the central stable `1.4.1` pin and contract tests.

## Development

The repository targets .NET 10 and uses locked NuGet restores:

```sh
dotnet restore --locked-mode
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
```

See [CONTRIBUTING.md](CONTRIBUTING.md) for clean-room and test requirements, [SECURITY.md](SECURITY.md) for private vulnerability reporting, and [LICENSE](LICENSE) for the MIT license.
