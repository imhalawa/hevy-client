# Architecture

This project is a single-tenant MCP server for the official Hevy API. It runs
locally over standard input/output by default and can optionally run behind a
user-managed TLS reverse proxy over Streamable HTTP.

## System boundary

```text
MCP client -> Hevy.Mcp -> Hevy.Core.Ports.IHevyClient <- Hevy.Client -> Hevy API
```

The server performs deterministic API access and calculations. It does not run
a model, provide coaching, persist fitness data, store credentials, or send
telemetry.

## Components

`Hevy.Core` owns pure domain models, domain exceptions, use-case inputs and
results, and the outbound `IHevyClient` port. Each use case keeps its models in
its own `UseCases/<UseCase>` directory. Core types contain no HTTP, JSON, Refit,
Polly, or MCP concerns.

`Hevy.Client` is the outbound adapter. It owns Hevy API request and response
contracts, explicit domain mapping, Refit endpoint declarations,
authentication, pagination, response validation, response-size limits, and
Polly retry policy. Release builds send authenticated requests only to the
fixed `https://api.hevyapp.com` origin.

`Hevy.Mcp` is the executable composition root and inbound adapter. It owns
configuration, transports, tool and prompt registration,
compact result projections, bounded analysis, process-local caching,
authorization, and redacted diagnostics. It invokes Core use cases through the
port and wires `Hevy.Client` as the production implementation.

Production source files contain one type. Collections exposed by project types
use `ImmutableList<T>`, not `IReadOnlyList<T>`.

## Transports

Stdio is the default. Standard output contains MCP protocol messages only, and
the process opens no network port.

HTTP mode is optional and single-tenant: one server instance represents one
Hevy account. It requires a separate bearer token, rejects a token equal to the
Hevy API key, and is intended to remain loopback-bound behind a TLS reverse
proxy. Its unauthenticated `/healthz` endpoint reveals only process liveness.

## Security invariants

- `HEVY_API_KEY` is supplied through the process environment and is never a
  tool argument, URL value, image layer, or persisted application setting.
- Logs exclude credentials, headers, payloads, response bodies, workout text,
  activity timestamps, and body measurements.
- `HEVY_READ_ONLY=true` prevents mutation tools from being registered.
- Mutations validate locally and support `dry_run`. Non-idempotent writes are
  never retried automatically.
- When a write may have committed but its response cannot be trusted, the
  server reports an outcome that must be read back instead of replaying it.
- Caches are bounded, process-local, omit credentials from keys, and invalidate
  affected entries before or after writes as required for safety.

## Bounded data access

Low-level tools preserve Hevy pagination. Composite tools may combine pages,
but every call has explicit item, time-range, byte, and page limits. Partial
results identify truncation and provide continuation inputs when the upstream
API supports safe resumption. Exercise history is unpaginated upstream, so it
fails closed at its scan or byte limit instead of pretending deeper continuation
is safe.

Structured MCP content is authoritative. Text output is a compact compatibility
view for clients that do not fully support structured results. Deterministic
analysis includes the source identifiers and timestamps needed to inspect its
evidence.

## Verification and evolution

The checked-in OpenAPI snapshot under `docs/api/` is the reviewed contract for
the Refit adapter and its contract tests. Updating it is an explicit
maintenance change; the server never fetches API documentation at runtime.

CI restores locked dependencies, verifies formatting, builds with warnings as
errors, runs unit and transport tests without real credentials, builds the
container, and checks reproducible multi-architecture output. Release consumers
pin the published image digest; verification commands and release evidence live
in [release-verification.md](release-verification.md).
