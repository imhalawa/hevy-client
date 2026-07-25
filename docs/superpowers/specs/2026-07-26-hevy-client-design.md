# Hevy Client Design

**Date:** 2026-07-26  
**Status:** Approved for autonomous implementation  
**Project:** `hevy-client`

## Purpose

`hevy-client` is a clean-room, open-source MCP server for the official Hevy API. It is designed primarily for AI agents while remaining predictable for human operators. It runs locally by default, can be self-hosted as a single-tenant service, sends authenticated fitness data only to Hevy, and contains no telemetry or embedded AI model.

The project is independent of `chrisdoc/hevy-mcp`. That repository may inform competitive analysis and tool ergonomics, but no source code is copied or adapted. The official Hevy API documentation and its OpenAPI document are the implementation contract.

## Chosen Approach

The implementation will use C# on .NET 10 LTS and the official Model Context Protocol C# SDK. This was selected over:

- Python, which would reduce initial implementation time but offers weaker compile-time guarantees and is undergoing an MCP SDK major-version transition.
- Rust, which would produce the smallest runtime and strongest compile-time guarantees but currently has a Tier 2 MCP SDK and would substantially increase implementation cost for the DTO and tool surface.
- TypeScript, which has strong MCP support but provides less runtime isolation and type safety than the selected .NET design.
- Forking the existing MCP server, which would inherit unrelated hosted-service, OAuth, telemetry, and workspace complexity.

## Scope

### Official API coverage

The typed client and low-level MCP tools will cover every operation in the checked-in Hevy OpenAPI snapshot, currently organized under:

- `/v1/workouts`, `/v1/workouts/count`, `/v1/workouts/events`, and `/v1/workouts/{workoutId}`
- `/v1/user/info`
- `/v1/routines` and `/v1/routines/{routineId}`
- `/v1/exercise_templates` and `/v1/exercise_templates/{exerciseTemplateId}`
- `/v1/routine_folders` and `/v1/routine_folders/{folderId}`
- `/v1/exercise_history/{exerciseTemplateId}`
- `/v1/body_measurements` and `/v1/body_measurements/{date}`

No delete operation will be invented where Hevy exposes none.

### Agent-oriented capabilities

In addition to one predictable low-level tool per API operation, the server will provide a deliberately small composite surface:

- Search routines by normalized title.
- Search exercise templates by normalized title and optional equipment or muscle filters.
- Retrieve bounded workout evidence across multiple Hevy pages.
- Summarize training frequency, volume, progression, consistency gaps, and body-measurement deltas for a bounded period.
- Summarize bounded history for a selected exercise template.

Composite tools perform deterministic calculations only. They return the workout, exercise, or measurement identifiers and timestamps supporting each result. The connected AI agent—not this server—provides subjective interpretation or coaching.

Two MCP prompts will guide common multi-step workflows without embedding model calls:

- Analyze recent training from deterministic evidence.
- Create a completed workout from a routine without inventing missing set results.

The first release will not expose bulk MCP resources. Tools provide more consistent cross-client behavior and prevent eager injection of large catalogs or histories.

## Architecture

The solution has two production projects with narrow interfaces:

1. `Hevy.Client` owns HTTP requests, authentication, DTOs, pagination, retry classification, response parsing, and Hevy error normalization. It has no MCP dependency.
2. `Hevy.Mcp` owns configuration, stdio and HTTP hosting, MCP tools/prompts, compact projections, deterministic analysis, caching, authorization, and diagnostics. It depends on `Hevy.Client` through an `IHevyClient` interface.

Tests are separated by boundary:

- Client tests exercise real serialization and request behavior through an injected fake `HttpMessageHandler`.
- MCP tests exercise tools against a fake `IHevyClient` without network traffic.
- Contract tests validate DTO fixtures and tool schemas against the checked-in official OpenAPI snapshot.
- Transport smoke tests start the built server and complete MCP initialization, tool discovery, and a representative tool call.

Production code uses dependency injection and focused files grouped by capability. The Hevy API origin is an internal constant in release code. Tests replace the HTTP transport itself rather than setting an alternate base URL.

## Data Flow

### Local stdio mode

```text
MCP client -> Docker stdin/stdout -> Hevy.Mcp -> Hevy.Client -> https://api.hevyapp.com
```

Stdio is the default transport. The client keeps the container process alive with stdin attached. No port is opened, and stdout is reserved for MCP protocol messages.

### Self-hosted HTTP mode

```text
MCP client -> TLS reverse proxy -> authenticated /mcp -> Hevy.Mcp -> Hevy.Client -> https://api.hevyapp.com
```

HTTP mode uses Streamable HTTP and is explicitly single-tenant: one container serves one Hevy account. It requires a separate MCP bearer token and refuses to start without it. The Hevy API key is never sent to MCP clients. Production documentation requires TLS termination. HTTP mode exposes an unauthenticated `GET /healthz` endpoint that returns only an empty `200 OK`; version and configuration details remain behind MCP authentication.

Browser-only or hosted agents are supported only when a user deliberately self-hosts HTTP mode. The project does not provide a hosted public service, tunnels, multi-user storage, OAuth authorization server, or API-key database.

## Configuration and Secrets

Configuration is environment-based:

- `HEVY_API_KEY` is required and is the only supported Hevy credential source.
- `HEVY_MCP_TRANSPORT` is `stdio` by default and accepts `http` for self-hosting.
- `MCP_AUTH_TOKEN` is required in HTTP mode and ignored in stdio mode.
- `HEVY_READ_ONLY` defaults to `false`; setting it to `true` prevents mutation tools from being registered.
- `HEVY_LOG_LEVEL` defaults to `None` and enables redacted stderr diagnostics when explicitly configured.

The Hevy key is never accepted through a tool argument, command-line flag, URL, source file, image layer, or persisted application file. Documentation passes an existing host environment variable into Docker rather than embedding the literal key in command history or MCP configuration.

Release builds can send authenticated requests only to `https://api.hevyapp.com`. No runtime base-URL override exists.

## Tool Contract

### Inputs and outputs

- Tool inputs and outputs use explicit JSON schemas derived from C# types.
- Outgoing mutation payloads are strictly validated before network access.
- Incoming responses tolerate unknown additive fields while still requiring fields needed by the operation.
- Collection, search, and analysis tools return compact projections by default, plus pagination metadata.
- Single-item tools return the complete known Hevy object.
- Collections accept a detail option when nested records are needed.
- Structured content is authoritative; a short text representation is also returned for clients with incomplete structured-output support.
- Errors contain a stable local error code, short actionable message, retryability, local correlation ID, and Hevy status/request identifier when available.

### Pagination and bounds

Hevy pages contain at most ten items. The server may combine pages for composite operations but never performs an unbounded fetch in one tool call.

- Low-level list tools preserve explicit page and page-size semantics.
- Composite collection tools default to 100 items and cap a single call at 1,000 items.
- Training summaries default to four weeks and cap a single call at 52 weeks.
- Every partial result sets `truncated: true` and returns explicit continuation inputs.
- An agent can repeatedly continue until it has enough evidence; the limit is per call, not per session.
- Cancellation from the MCP client stops outstanding Hevy requests.

### Mutation safety

Writes are available by default unless `HEVY_READ_ONLY=true`.

- Every mutation is marked with the applicable MCP mutation/destructive/idempotency annotations.
- Every mutation supports `dry_run`, returning the normalized outbound payload and validation warnings without calling Hevy.
- A real mutation does not require a prior dry run; the MCP host remains responsible for presenting human approval when supported.
- Composite replacement tools require `expected_updated_at`. They fetch current state immediately before writing and return a conflict if it changed.
- Low-level replacement tools accept the same guard but permit an explicit force override.
- Cache entries affected by a successful mutation are invalidated immediately.

## Caching and Performance

Caching is bounded and process-local. Nothing is persisted across container restarts.

- Exercise-template and routine reads use a 15-minute sliding TTL.
- Other account data is not cached in the first release.
- Successful writes invalidate related entries.
- Cache keys never contain the API key.
- Cache size is bounded so a long-running HTTP deployment cannot grow without limit.

The server uses asynchronous I/O throughout, reuses a single managed `HttpClient`, propagates cancellation, and avoids reflection-heavy or generated-client layers in the request path.

## Retry and Failure Semantics

Read requests retry transient connection failures, `429`, and selected `5xx` responses at most twice after the original attempt. Backoff is exponential with jitter and honors `Retry-After` when it fits within the operation timeout.

Non-idempotent `POST` mutations are never retried automatically. Replacement-style `PUT` operations are retried only when the client can prove the request is idempotent. When a connection fails after a mutation may have reached Hevy, the server returns `outcome_unknown` and directs the agent to read back current state before attempting another write.

Hevy errors map into stable categories including authentication, authorization, validation, not found, conflict, rate limited, transient upstream failure, timeout, outcome unknown, and unexpected response. The original sensitive body is not echoed to the model or logs.

## Privacy and Diagnostics

The server contains no telemetry, analytics, crash reporter, update checker, or unsolicited network call.

Normal stdio mode writes only MCP messages to stdout. Opt-in diagnostics write redacted records to stderr containing only server version, runtime version, operation name, duration, HTTP status, safe Hevy request/error identifier, local correlation ID, and exception category.

Diagnostics never include headers, credentials, URLs with parameters, payloads, response bodies, workout or exercise text, timestamps from a user's activity, or body measurements. A `get_diagnostics` tool returns only non-sensitive runtime, transport, feature-mode, and health information. Users copy diagnostic output into issue reports themselves; the server never uploads it.

## OpenAPI Lifecycle

A dated JSON snapshot of the official OpenAPI document is checked into `docs/api/`. It is provenance, a contract-test input, and a reviewable record—not a code-generation input.

The HTTP client and DTOs are hand-written. Updating the snapshot is an explicit maintainer action followed by contract tests and a reviewed diff. Additive response fields remain compatible; changed or removed fields fail contract tests before release. No runtime request fetches documentation or checks for updates.

## Testing and Quality Gates

Development follows test-driven development for production behavior.

Required automated coverage includes:

- Authentication header injection and secret redaction.
- Serialization and validation for every official request/response family.
- All low-level tools and composite calculations.
- Pagination, continuation, bounds, cancellation, retry classification, and ambiguous mutations.
- Read-only mode and MCP mutation annotations.
- Optimistic concurrency conflicts.
- Cache hits, expiry, bounds, and invalidation.
- Stdio protocol cleanliness and HTTP bearer authentication.
- Diagnostic redaction.

The default test suite uses only fake transports and sanitized fixtures. Optional live smoke tests run only when explicitly enabled and `HEVY_API_KEY` is already present. Live tests are read-only unless a second, separate mutation opt-in is enabled. CI never receives or requires a Hevy API key.

Before release, CI must pass formatting, analyzers with warnings treated as errors, unit tests, contract tests, transport smoke tests, Release builds, and a container smoke test.

## Packaging and Distribution

The repository is public and MIT licensed. A multi-stage Dockerfile produces a minimal .NET 10 chiseled runtime image that:

- Runs as the built-in non-root user.
- Contains no shell or package manager.
- Contains only published application artifacts and runtime dependencies.
- Supports `linux/amd64` and `linux/arm64`.
- Includes OCI source, revision, version, license, and description labels.

GitHub releases use semantic versioning. GitHub Container Registry publishes immutable version tags and digests. Release workflows generate an SBOM and provenance attestations and sign images keylessly. Documentation recommends pinning a semantic version or digest rather than relying on automatic `latest` upgrades.

The README includes Docker MCP configuration examples for Codex, Claude Desktop, Cursor, VS Code, Gemini CLI, and generic stdio clients, plus a secure reverse-proxy example for Streamable HTTP. Client-specific configuration does not change protocol behavior.

## Explicit Non-Goals

- Hosting a shared public Hevy MCP service.
- Multi-tenant accounts or persisted credential storage.
- OAuth issuance or browser-based credential capture.
- Persistent workout, routine, template, or measurement caches.
- Embedded LLM calls, coaching, or subjective conclusions.
- Invented delete operations.
- Automatic API-spec or application update checks.
- Compatibility shims that violate the MCP standard for one client.

## Acceptance Criteria

The first release is acceptable when:

1. Every operation in the pinned Hevy OpenAPI snapshot has a tested typed-client method and MCP tool.
2. Composite search, bounded evidence retrieval, exercise-history summary, and training summary tools return deterministic structured results with continuations and evidence identifiers.
3. Stdio works in a Docker-backed MCP configuration without opening a port.
4. Streamable HTTP refuses unauthenticated requests and refuses startup without `MCP_AUTH_TOKEN`.
5. Read-only mode omits all mutation tools; default mode includes them with correct annotations and dry-run behavior.
6. Tests demonstrate retry safety, ambiguous-write handling, optimistic concurrency, cache invalidation, cancellation, and diagnostic redaction.
7. The process never persists user fitness data or secrets and never contacts a non-Hevy service at runtime.
8. The non-root multi-architecture container passes its smoke test.
9. Documentation lets a user configure at least Codex, Claude Desktop, Cursor, VS Code, and Gemini CLI without exposing the Hevy key to the model.
10. CI can build and test the entire repository without a real Hevy account or API key.

## Primary References

- Official Hevy API documentation: <https://api.hevyapp.com/docs/>
- Official MCP SDK tier list: <https://modelcontextprotocol.io/docs/sdk>
- Official MCP C# SDK: <https://github.com/modelcontextprotocol/csharp-sdk>
- .NET support policy: <https://dotnet.microsoft.com/en-us/platform/support/policy>
