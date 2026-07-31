# Contributing

Thank you for helping improve `hevy-client`. Contributions must preserve its clean-room, local-first security model.

## Clean-room rule

Use the checked-in official Hevy OpenAPI snapshot, official Hevy documentation, the MCP specification, and official SDK documentation as implementation sources. Do not copy or adapt code from other Hevy MCP servers. Record a primary-source link when a contract change is not evident from the snapshot.

Never add a real Hevy API key, bearer token, workout, routine, activity timestamp, or body measurement to a fixture, test result, issue, or commit. Use conspicuously synthetic identifiers such as `workout-1` and `fixture-key`.

## Local setup

Install the SDK feature band selected by `global.json`, then run:

```sh
dotnet restore --locked-mode
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
dotnet format --verify-no-changes --no-restore
```

The normal suite uses fake transports and needs no Hevy account. Live tests are opt-in and must remain read-only unless their separate mutation gate is explicitly enabled; never introduce a CI dependency on live credentials.

Docker changes also require:

```sh
docker build --pull --tag hevy-client:contributor .
dotnet test tests/Hevy.Transport.Tests/Hevy.Transport.Tests.csproj \
  --configuration Release \
  --filter FullyQualifiedName~ContainerSmokeTests
```

The smoke tests build an image, inspect its non-root/no-port configuration, complete a real stdio MCP handshake, and exercise loopback-only HTTP health and bearer rejection. They dynamically skip only when the Docker executable or daemon is genuinely unavailable.

## Development process

1. Describe the public behavior and the test seam first.
2. Add one behavior test and observe it fail for the expected reason.
3. Implement the smallest vertical slice that makes it pass.
4. Repeat, then review and simplify without weakening the contract.
5. Run the focused suite, the full Release suite, formatting, and `git diff --check`.

Test through public seams: typed HTTP requests through an injected handler, MCP calls through a fake `IHevyClient`, or real process/container transports. Avoid tests coupled to private implementation details or assertions that recompute the production result.

Use FluentAssertions for every test assertion. The repository audit rejects `Assert.*`. C# source also rejects `//` comments; remove narration and stale implementation notes. When a non-obvious compatibility contract must survive refactoring, document the reason and re-audit condition with XML documentation.

Keep `Hevy.Client` free of MCP dependencies. Keep environment and hosting concerns in `Hevy.Mcp`. Do not add telemetry, runtime documentation fetches, persistent fitness-data storage, configurable production API origins, API-key tool arguments, unbounded fetches, invented endpoints, or automatic mutation retries without a proof of idempotency.

## Updating the Hevy contract

The dated file under `docs/api/` is provenance and a contract-test input, not a code-generation source. Update it only as an explicit maintainer task from the official Hevy documentation. Review the normalized diff, update handwritten DTOs/client methods/tools, and prove every changed operation through contract and transport tests. The application must never fetch API documentation or check for updates at runtime.

## Updating container bases

The .NET SDK and noble-chiseled runtime are pinned by immutable multi-architecture manifest digests. Resolve current official digests with:

```sh
docker buildx imagetools inspect mcr.microsoft.com/dotnet/sdk:10.0-noble
docker buildx imagetools inspect mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled
```

Confirm the SDK still satisfies `global.json`, review Microsoft's release and image notes, replace both Dockerfile digests together when appropriate, and rerun all Release and container checks. Do not replace a digest pin with a mutable-only tag.

Release builds also pass non-secret `VERSION`, `REVISION`, and `SOURCE_URL` build arguments so the OCI labels identify the published source exactly. Local defaults intentionally identify a development build.

## Pull requests

Keep changes focused and explain the user-visible behavior, security implications, RED evidence, and verification commands. Update durable documentation when configuration or a limitation changes. All compiler and analyzer warnings are errors.

Dependency updates are reviewed changes, never automatic merges. Dependabot groups weekly minor and patch updates; major updates remain separate and require explicit maintainer approval. Changes to GitHub Actions must retain full-commit pins and update `.github/actions-lock.json` with the reviewed version and source. Do not select an MCP 2.x preview through routine dependency maintenance; that requires a dedicated migration plan, contract review, and central version-pin change.

By contributing, you agree that your contribution is licensed under the repository's MIT license. Report suspected vulnerabilities through the private process in [SECURITY.md](SECURITY.md), not in a public pull request.
