# Hevy Client Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a clean-room, production-ready .NET 10 MCP server with complete typed coverage of the official Hevy API, deterministic agent-oriented tools, local stdio, and authenticated single-tenant HTTP hosting.

**Architecture:** `Hevy.Client` is a transport-agnostic typed HTTP client with no MCP dependency. `Hevy.Mcp` composes that client into MCP tools, prompts, bounded analysis, cache, diagnostics, and stdio/HTTP hosts. Tests use injected HTTP handlers and fake client implementations; no default test contacts Hevy.

**Tech Stack:** C# 14, .NET 10 LTS, ASP.NET Core 10, ModelContextProtocol 1.4.1, ModelContextProtocol.AspNetCore 1.4.1, System.Text.Json, Microsoft.Extensions.Caching.Memory, xUnit 2.9.3, Microsoft.NET.Test.Sdk 18.8.1, Docker BuildKit, GitHub Actions.

## Global Constraints

- Use only stable dependencies; pin `ModelContextProtocol` packages to `1.4.1` rather than the 2.0 preview line.
- Target `net10.0`; set nullable and implicit usings on, analyzers to latest, and warnings as errors.
- `HEVY_API_KEY` is the only Hevy credential source and must never be logged, serialized into MCP content, or stored.
- Release code can send authenticated requests only to `https://api.hevyapp.com`.
- Default transport is stdio; HTTP mode is single-tenant and requires `MCP_AUTH_TOKEN`.
- Writes are enabled by default and omitted when `HEVY_READ_ONLY=true`.
- Production behavior follows red-green-refactor; every behavior test must be observed failing before its implementation is written.
- After each task: update `docs/superpowers/progress/2026-07-26-hevy-client-progress.md`, run the task verification, and commit code plus ledger together.
- Do not copy source code from `chrisdoc/hevy-mcp`; use only official Hevy and MCP contracts.

---

## File Map

### Repository configuration

- `HevyClient.slnx` — solution membership.
- `global.json` — .NET 10 SDK selection with latest-patch roll-forward.
- `Directory.Build.props` — common compiler, analyzer, deterministic-build, and package-lock settings.
- `Directory.Packages.props` — centrally pinned NuGet versions.
- `.editorconfig`, `.gitignore`, `LICENSE` — repository policy and MIT license.
- `docs/api/hevy-openapi-2026-07-26.json` — exact official contract snapshot.

### Typed client

- `src/Hevy.Client/Hevy.Client.csproj` — dependency-light class library.
- `src/Hevy.Client/IHevyClient.cs` — public async API used by MCP.
- `src/Hevy.Client/HevyClient.cs` — endpoint orchestration.
- `src/Hevy.Client/HevyClientOptions.cs` — API-key and timeout validation; API origin remains internal.
- `src/Hevy.Client/Http/HevyAuthenticationHandler.cs` — `api-key` header injection.
- `src/Hevy.Client/Http/HevyRetryHandler.cs` — conservative read/idempotent retry policy.
- `src/Hevy.Client/Http/HevyResponse.cs` — response/error parsing and ambiguity classification.
- `src/Hevy.Client/Errors/HevyException.cs` — stable error categories.
- `src/Hevy.Client/Models/Common.cs` — pagination and shared set fields.
- `src/Hevy.Client/Models/Workouts.cs` — workout DTOs and requests.
- `src/Hevy.Client/Models/Routines.cs` — routine and folder DTOs/requests.
- `src/Hevy.Client/Models/Exercises.cs` — templates and history DTOs/requests.
- `src/Hevy.Client/Models/Measurements.cs` — body-measurement DTOs/requests.
- `src/Hevy.Client/Models/User.cs` — user-info DTOs.
- `src/Hevy.Client/Serialization/HevyJsonContext.cs` — source-generated JSON metadata.

### MCP server

- `src/Hevy.Mcp/Hevy.Mcp.csproj` — executable ASP.NET Core project.
- `src/Hevy.Mcp/Program.cs` — thin entry point selecting stdio or HTTP.
- `src/Hevy.Mcp/Configuration/HevyMcpOptions.cs` — environment parsing and validation.
- `src/Hevy.Mcp/Hosting/ServiceRegistration.cs` — DI and MCP registration.
- `src/Hevy.Mcp/Hosting/StdioHost.cs` — stdio lifecycle.
- `src/Hevy.Mcp/Hosting/HttpHost.cs` — bearer-protected Streamable HTTP and `/healthz`.
- `src/Hevy.Mcp/Tools/*Tools.cs` — low-level tools grouped by Hevy capability.
- `src/Hevy.Mcp/Tools/ToolResults.cs` — structured success/error envelopes.
- `src/Hevy.Mcp/Tools/ToolExceptionFilter.cs` — stable MCP error conversion.
- `src/Hevy.Mcp/Composite/SearchService.cs` — normalized routine/template search.
- `src/Hevy.Mcp/Composite/TrainingAnalysisService.cs` — deterministic bounded metrics.
- `src/Hevy.Mcp/Composite/Continuation.cs` — continuation validation/creation.
- `src/Hevy.Mcp/Caching/HevyCache.cs` — bounded memory cache and invalidation.
- `src/Hevy.Mcp/Diagnostics/DiagnosticSnapshot.cs` — safe diagnostics projection.
- `src/Hevy.Mcp/Prompts/HevyPrompts.cs` — two guided prompts.

### Tests and delivery

- `tests/Hevy.Client.Tests/**` — client, serialization, auth, retry, and error tests.
- `tests/Hevy.Mcp.Tests/**` — options, tools, composites, cache, diagnostics, and HTTP tests.
- `tests/Hevy.Transport.Tests/**` — real-process MCP smoke tests.
- `tests/TestSupport/**` — sanitized fixtures, recording handler, fake client, and clocks.
- `Dockerfile`, `.dockerignore` — hardened multi-stage image.
- `README.md`, `SECURITY.md`, `CONTRIBUTING.md` — operator and contributor docs.
- `.github/workflows/ci.yml` — format/build/test/container checks.
- `.github/workflows/release.yml` — multi-architecture GHCR image, SBOM, provenance, and keyless signing.

---

### Task 1: Reproducible repository foundation and official API snapshot

**Files:**
- Create: `HevyClient.slnx`
- Create: `global.json`
- Create: `Directory.Build.props`
- Create: `Directory.Packages.props`
- Create: `.editorconfig`
- Create: `.gitignore`
- Create: `LICENSE`
- Create: `docs/api/hevy-openapi-2026-07-26.json`
- Create: `src/Hevy.Client/Hevy.Client.csproj`
- Create: `src/Hevy.Mcp/Hevy.Mcp.csproj`
- Create: `tests/Hevy.Client.Tests/Hevy.Client.Tests.csproj`
- Create: `tests/Hevy.Mcp.Tests/Hevy.Mcp.Tests.csproj`
- Create: `tests/Hevy.Transport.Tests/Hevy.Transport.Tests.csproj`
- Create: `docs/superpowers/progress/2026-07-26-hevy-client-progress.md`

**Interfaces:**
- Consumes: Approved design spec and official Swagger document embedded at `https://api.hevyapp.com/docs/swagger-ui-init.js`.
- Produces: Buildable empty solution, pinned packages, normalized OpenAPI JSON, and durable task ledger.

- [ ] **Step 1: Add the repository configuration**

The workspace currently has .NET 9 only. Install the latest GA .NET 10 SDK side-by-side with the official `dotnet-install.sh` into `/home/atom/.dotnet`, then confirm `dotnet --list-sdks` includes a `10.0.3xx` or newer stable feature band before generating project files.

Create `global.json` with an installed .NET 10 feature band and safe roll-forward:

```json
{
  "sdk": {
    "version": "10.0.300",
    "rollForward": "latestFeature",
    "allowPrerelease": false
  }
}
```

Set `TargetFramework=net10.0`, `TreatWarningsAsErrors=true`, `AnalysisLevel=latest`, `ContinuousIntegrationBuild` from `CI`, deterministic output, and restore lock files in `Directory.Build.props`. Pin the package versions listed in the header in `Directory.Packages.props`.

- [ ] **Step 2: Capture and validate the official OpenAPI snapshot**

Extract `options.swaggerDoc` from the official Swagger initializer into stable, two-space-indented JSON. Verify the snapshot has version `0.0.1`, exactly the 14 expected `/v1/` paths from the design, no server origin other than Hevy, and no credential value.

Run:

```bash
jq -e '.info.version == "0.0.1" and (.paths | length == 14)' docs/api/hevy-openapi-2026-07-26.json
```

Expected: exit 0 and `true`.

- [ ] **Step 3: Create the projects and solution membership**

Use SDK-style projects. `Hevy.Client` is a class library. `Hevy.Mcp` uses `Microsoft.NET.Sdk.Web`, references `Hevy.Client`, `ModelContextProtocol`, and `ModelContextProtocol.AspNetCore`. Test projects reference their respective production projects and the shared xUnit packages.

- [ ] **Step 4: Restore and build the empty solution**

Run:

```bash
dotnet restore --use-lock-file
dotnet build --no-restore -c Release
```

Expected: restore and build exit 0 with zero warnings.

- [ ] **Step 5: Seed the durable progress ledger and commit**

The ledger records the current commit, completed task, verification command/results, next task, and any discovered constraint. Commit with:

```bash
git add .
git commit -m "chore: establish .NET solution and API contract"
```

---

### Task 2: Typed models and serialization contract

**Files:**
- Create: `src/Hevy.Client/Models/Common.cs`
- Create: `src/Hevy.Client/Models/Workouts.cs`
- Create: `src/Hevy.Client/Models/Routines.cs`
- Create: `src/Hevy.Client/Models/Exercises.cs`
- Create: `src/Hevy.Client/Models/Measurements.cs`
- Create: `src/Hevy.Client/Models/User.cs`
- Create: `src/Hevy.Client/Serialization/HevyJsonContext.cs`
- Create: `tests/TestSupport/Fixtures/*.json`
- Create: `tests/Hevy.Client.Tests/Serialization/*Tests.cs`

**Interfaces:**
- Consumes: `docs/api/hevy-openapi-2026-07-26.json`.
- Produces: Immutable request/response records and `HevyJsonContext.Default` for every DTO family.

- [ ] **Step 1: Write failing response-deserialization tests**

For each schema family, deserialize a sanitized official-shaped fixture and assert meaningful typed fields. The workout test establishes the pattern:

```csharp
[Fact]
public void Workout_accepts_unknown_additive_fields()
{
    var json = Fixture.Read("workout.json").Replace("\"title\":", "\"future_field\":42,\"title\":");
    var workout = JsonSerializer.Deserialize(json, HevyJsonContext.Default.Workout);

    Assert.Equal("workout-1", workout!.Id);
    Assert.Equal("Bench Press (Barbell)", workout.Exercises[0].Title);
}
```

- [ ] **Step 2: Run the serialization tests and verify RED**

Run `dotnet test tests/Hevy.Client.Tests --filter FullyQualifiedName~Serialization`. Expected: compile failure because the model/context types do not exist.

- [ ] **Step 3: Implement immutable DTOs and source-generated metadata**

Use `sealed record` types, `JsonPropertyName` where C# names differ, nullable values only where the contract allows null, and `IReadOnlyList<T>` collection properties. Model set metrics once and reuse them across workout, routine, and history records.

- [ ] **Step 4: Write failing request-serialization tests**

Assert every create/update family produces exact snake_case property names and omits server-owned fields:

```csharp
[Fact]
public void CreateWorkout_serializes_only_writable_fields()
{
    var request = FixtureFactory.CreateWorkoutRequest();
    var json = JsonSerializer.Serialize(request, HevyJsonContext.Default.CreateWorkoutRequest);

    Assert.Contains("\"start_time\"", json);
    Assert.DoesNotContain("updated_at", json, StringComparison.Ordinal);
}
```

- [ ] **Step 5: Run GREEN verification and commit**

Run `dotnet test tests/Hevy.Client.Tests --filter FullyQualifiedName~Serialization`. Expected: all serialization tests pass. Update the ledger and commit with `feat: model Hevy API contracts`.

---

### Task 3: Authenticated read client, pagination, cancellation, and errors

**Files:**
- Create: `src/Hevy.Client/IHevyClient.cs`
- Create: `src/Hevy.Client/HevyClient.cs`
- Create: `src/Hevy.Client/HevyClientOptions.cs`
- Create: `src/Hevy.Client/Http/HevyAuthenticationHandler.cs`
- Create: `src/Hevy.Client/Http/HevyResponse.cs`
- Create: `src/Hevy.Client/Errors/HevyException.cs`
- Create: `tests/TestSupport/RecordingHttpMessageHandler.cs`
- Create: `tests/Hevy.Client.Tests/HevyClientReadTests.cs`
- Create: `tests/Hevy.Client.Tests/HevyClientErrorTests.cs`

**Interfaces:**
- Consumes: Typed models and `HttpClient` supplied by DI.
- Produces: `IHevyClient` methods for every official GET endpoint.

Define the stable interface explicitly:

```csharp
public interface IHevyClient
{
    Task<PagedResult<Workout>> GetWorkoutsAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<int> GetWorkoutCountAsync(CancellationToken cancellationToken);
    Task<PagedResult<WorkoutEvent>> GetWorkoutEventsAsync(int page, int pageSize, DateTimeOffset since, CancellationToken cancellationToken);
    Task<Workout> GetWorkoutAsync(string workoutId, CancellationToken cancellationToken);
    Task<UserInfo> GetUserInfoAsync(CancellationToken cancellationToken);
    Task<PagedResult<Routine>> GetRoutinesAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<Routine> GetRoutineAsync(string routineId, CancellationToken cancellationToken);
    Task<PagedResult<ExerciseTemplate>> GetExerciseTemplatesAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<ExerciseTemplate> GetExerciseTemplateAsync(string exerciseTemplateId, CancellationToken cancellationToken);
    Task<PagedResult<RoutineFolder>> GetRoutineFoldersAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<RoutineFolder> GetRoutineFolderAsync(long folderId, CancellationToken cancellationToken);
    Task<PagedResult<ExerciseHistoryEntry>> GetExerciseHistoryAsync(string exerciseTemplateId, int page, int pageSize, DateOnly? startDate, DateOnly? endDate, CancellationToken cancellationToken);
    Task<PagedResult<BodyMeasurement>> GetBodyMeasurementsAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<BodyMeasurement> GetBodyMeasurementAsync(DateOnly date, CancellationToken cancellationToken);
}
```

- [ ] **Step 1: Write failing authentication and URI tests**

Assert the handler sends `api-key` exactly once, uses `https://api.hevyapp.com/v1/...`, validates page values before network access, and never includes the key in `ToString()` or exceptions.

- [ ] **Step 2: Run tests and verify RED**

Run `dotnet test tests/Hevy.Client.Tests --filter FullyQualifiedName~HevyClientReadTests`. Expected: compile failure for missing client types.

- [ ] **Step 3: Implement read methods and strict option validation**

Construct relative URIs only, encode identifiers and query values, set the fixed origin on the supplied `HttpClient`, propagate cancellation, and deserialize with `HevyJsonContext`.

- [ ] **Step 4: Write failing stable-error tests**

Exercise `401/403/404/409/429/500`, malformed JSON, empty bodies, and cancellation. Assert `HevyException.Code`, `IsRetryable`, `StatusCode`, and safe message without response body leakage.

- [ ] **Step 5: Implement response normalization, verify, and commit**

Run all client tests. Expected: pass with zero warnings. Update the ledger and commit with `feat: add authenticated Hevy read client`.

---

### Task 4: Safe mutations and conservative retry pipeline

**Files:**
- Modify: `src/Hevy.Client/IHevyClient.cs`
- Modify: `src/Hevy.Client/HevyClient.cs`
- Create: `src/Hevy.Client/Http/HevyRetryHandler.cs`
- Create: `src/Hevy.Client/Errors/HevyOutcomeUnknownException.cs`
- Create: `tests/Hevy.Client.Tests/HevyClientMutationTests.cs`
- Create: `tests/Hevy.Client.Tests/HevyRetryHandlerTests.cs`

**Interfaces:**
- Consumes: Request DTOs, `HevyResponse`, and authenticated `HttpClient`.
- Produces: Create/update methods for every official POST/PUT endpoint and method-aware retry behavior.

Add these method families to `IHevyClient`:

```csharp
Task<Workout> CreateWorkoutAsync(CreateWorkoutRequest request, CancellationToken cancellationToken);
Task<Workout> UpdateWorkoutAsync(string workoutId, UpdateWorkoutRequest request, CancellationToken cancellationToken);
Task<Routine> CreateRoutineAsync(CreateRoutineRequest request, CancellationToken cancellationToken);
Task<Routine> UpdateRoutineAsync(string routineId, UpdateRoutineRequest request, CancellationToken cancellationToken);
Task<RoutineFolder> CreateRoutineFolderAsync(CreateRoutineFolderRequest request, CancellationToken cancellationToken);
Task<ExerciseTemplate> CreateExerciseTemplateAsync(CreateExerciseTemplateRequest request, CancellationToken cancellationToken);
Task<BodyMeasurement> CreateBodyMeasurementAsync(CreateBodyMeasurementRequest request, CancellationToken cancellationToken);
Task<BodyMeasurement> UpdateBodyMeasurementAsync(DateOnly date, UpdateBodyMeasurementRequest request, CancellationToken cancellationToken);
```

- [ ] **Step 1: Write failing mutation validation and method tests**

Assert invalid bodies make zero HTTP calls; valid calls use the exact official verb/path/body; identifiers are escaped; cancellation is propagated.

- [ ] **Step 2: Verify RED and implement the minimal mutation methods**

Run the mutation test filter, confirm missing-method failures, then implement only the tested endpoint behavior.

- [ ] **Step 3: Write failing retry-safety tests**

Cover GET retry on connection error, `429` with `Retry-After`, and `503`; maximum three total attempts; no POST retry; PUT retry only when marked safe; cancellation during delay; a failed response after body transmission maps to `outcome_unknown`.

- [ ] **Step 4: Implement retry policy with injectable delay/jitter collaborators**

Keep default production delay random and monotonic, but inject deterministic functions in tests. Never clone or retain secret-bearing request content longer than the operation.

- [ ] **Step 5: Verify and commit**

Run `dotnet test tests/Hevy.Client.Tests`. Expected: all client tests pass. Update the ledger and commit with `feat: add safe Hevy mutations and retries`.

---

### Task 5: MCP configuration, stdio host, and authenticated HTTP host

**Files:**
- Create: `src/Hevy.Mcp/Program.cs`
- Create: `src/Hevy.Mcp/Configuration/HevyMcpOptions.cs`
- Create: `src/Hevy.Mcp/Hosting/ServiceRegistration.cs`
- Create: `src/Hevy.Mcp/Hosting/StdioHost.cs`
- Create: `src/Hevy.Mcp/Hosting/HttpHost.cs`
- Create: `tests/Hevy.Mcp.Tests/Configuration/HevyMcpOptionsTests.cs`
- Create: `tests/Hevy.Mcp.Tests/Hosting/HttpHostTests.cs`
- Create: `tests/Hevy.Transport.Tests/StdioHandshakeTests.cs`

**Interfaces:**
- Consumes: `IHevyClient`, environment variables, ModelContextProtocol 1.4.1 hosting APIs.
- Produces: `Task<int> Program.Main(string[] args)`, stdio MCP transport, `/mcp`, and `/healthz`.

- [ ] **Step 1: Write failing options tests**

Assert missing/blank `HEVY_API_KEY` fails before host startup, default transport is stdio, only `stdio|http` are accepted, `HEVY_READ_ONLY` is strict boolean, and HTTP requires a distinct non-empty `MCP_AUTH_TOKEN`.

- [ ] **Step 2: Verify RED and implement immutable option parsing**

Keep secret properties out of generated `ToString()` output and diagnostic projections.

- [ ] **Step 3: Write failing stdio handshake test**

Start the built executable with a fake key, send MCP `initialize`, send `notifications/initialized`, request `tools/list`, and assert stdout contains only framed JSON-RPC messages while stderr remains empty at default log level.

- [ ] **Step 4: Implement stdio host with the official SDK**

Use `AddMcpServer().WithStdioServerTransport()` and DI-based tool registration. Ensure startup validation errors go to stderr and return nonzero without protocol noise.

- [ ] **Step 5: Write failing HTTP security tests**

Using `WebApplicationFactory`, assert `/healthz` returns empty `200`; `/mcp` rejects missing, malformed, and wrong bearer tokens; correct token reaches MCP; allowed-host and origin checks reject unsafe values; HTTP mode cannot start without its token.

- [ ] **Step 6: Implement stateless Streamable HTTP, verify, and commit**

Map the MCP endpoint at `/mcp`, apply constant-time bearer comparison, and leave `/healthz` outside authentication with an empty body. Run MCP and transport test projects, update the ledger, and commit with `feat: host MCP over stdio and authenticated HTTP`.

---

### Task 6: Complete low-level MCP tool surface

**Files:**
- Create: `src/Hevy.Mcp/Tools/ToolResults.cs`
- Create: `src/Hevy.Mcp/Tools/ToolExceptionFilter.cs`
- Create: `src/Hevy.Mcp/Tools/WorkoutTools.cs`
- Create: `src/Hevy.Mcp/Tools/RoutineTools.cs`
- Create: `src/Hevy.Mcp/Tools/ExerciseTools.cs`
- Create: `src/Hevy.Mcp/Tools/MeasurementTools.cs`
- Create: `src/Hevy.Mcp/Tools/UserTools.cs`
- Create: `tests/TestSupport/FakeHevyClient.cs`
- Create: `tests/Hevy.Mcp.Tests/Tools/*ToolTests.cs`
- Create: `tests/Hevy.Mcp.Tests/Tools/ToolInventoryTests.cs`

**Interfaces:**
- Consumes: Every `IHevyClient` method and server `ReadOnly` option.
- Produces: One snake_case MCP tool per official API operation, structured results, annotations, dry runs, and stable errors.

- [ ] **Step 1: Write a failing inventory contract test**

Parse the OpenAPI snapshot, map each HTTP operation to the exact expected tool name, invoke MCP `tools/list`, and assert there is exactly one low-level tool for each operation. Assert read-only mode omits POST/PUT tools without removing reads.

- [ ] **Step 2: Write failing representative read-tool tests**

Assert list tools validate `page >= 1` and `1 <= page_size <= 10`, return compact results and pagination metadata, accept `detail=full`, expose `ReadOnlyHint=true` and `OpenWorldHint=true`, and propagate cancellation.

- [ ] **Step 3: Implement read tools capability by capability**

Use focused static MCP tool classes with constructor/parameter DI supported by the official SDK. Descriptions state units, UTC/date semantics, pagination, and whether full nested data is returned.

- [ ] **Step 4: Write failing mutation-tool tests**

For each create/update tool assert local validation, `dry_run=true` makes zero client calls and returns normalized payload, real calls make one call, annotations distinguish additive POST from destructive replacement PUT, and errors use the stable envelope.

- [ ] **Step 5: Implement mutations and exception mapping**

All write tools accept `dry_run=false` by default. Update tools accept `expected_updated_at` and `force`; low-level force bypass is explicit in schema and result metadata.

- [ ] **Step 6: Verify complete surface and commit**

Run the MCP tests and a real `tools/list` transport smoke test. Expected: every OpenAPI operation covered and schemas parse in MCP Inspector-compatible JSON. Update ledger and commit with `feat: expose complete Hevy MCP tool surface`.

---

### Task 7: Cache, continuations, search, deterministic analysis, and prompts

**Files:**
- Create: `src/Hevy.Mcp/Caching/HevyCache.cs`
- Create: `src/Hevy.Mcp/Composite/Continuation.cs`
- Create: `src/Hevy.Mcp/Composite/SearchService.cs`
- Create: `src/Hevy.Mcp/Composite/TrainingAnalysisService.cs`
- Create: `src/Hevy.Mcp/Tools/CompositeTools.cs`
- Create: `src/Hevy.Mcp/Prompts/HevyPrompts.cs`
- Create: `tests/Hevy.Mcp.Tests/Caching/HevyCacheTests.cs`
- Create: `tests/Hevy.Mcp.Tests/Composite/ContinuationTests.cs`
- Create: `tests/Hevy.Mcp.Tests/Composite/SearchServiceTests.cs`
- Create: `tests/Hevy.Mcp.Tests/Composite/TrainingAnalysisServiceTests.cs`
- Create: `tests/Hevy.Mcp.Tests/Prompts/HevyPromptsTests.cs`

**Interfaces:**
- Consumes: `IHevyClient`, low-level DTOs, `TimeProvider`, and `IMemoryCache`.
- Produces: `search_routines`, `search_exercise_templates`, `get_workout_evidence`, `summarize_training`, `summarize_exercise_history`, and two MCP prompts.

- [ ] **Step 1: Write failing cache tests**

Assert routine/template requests coalesce concurrent loads, slide for 15 minutes, expire under fake time, stay within size limit, never key on credentials, and invalidate after related mutations.

- [ ] **Step 2: Implement bounded cache and verify GREEN**

Cache immutable DTOs only. Do not cache workouts, history, measurements, errors, or partial pages masquerading as full catalogs.

- [ ] **Step 3: Write failing continuation tests**

Assert signed-free opaque continuations encode only endpoint, next page, original filters, and remaining item budget; reject malformed, mismatched, or over-limit inputs; return `truncated=true` whenever more data exists.

- [ ] **Step 4: Implement continuation and normalized search**

Normalize with invariant case folding and whitespace collapse. Search returns compact IDs/titles and exact filter metadata, defaults to 100 results, and hard-caps at 1,000 per call.

- [ ] **Step 5: Write failing deterministic-analysis tests**

Use fixed workouts across UTC week boundaries and assert frequency, per-exercise volume (`weight_kg * reps` only when both exist), progression, missing-week gaps, measurement deltas, evidence IDs, default four-week range, 52-week cap, and continuation behavior. Assert no coaching adjectives or model-generated text appears.

- [ ] **Step 6: Implement analysis and optimistic composite updates**

Use `TimeProvider` and explicit UTC ranges. Before any composite replacement write, re-fetch and compare `updated_at`; return conflict with current metadata on mismatch.

- [ ] **Step 7: Add prompt contract tests and prompts**

The analysis prompt instructs the client model to cite returned evidence. The routine-to-workout prompt requires collection of actual completed-set results and end time before calling a mutation.

- [ ] **Step 8: Verify and commit**

Run all MCP tests, update the ledger, and commit with `feat: add agent-oriented Hevy workflows`.

---

### Task 8: Privacy-safe diagnostics and live-test guards

**Files:**
- Create: `src/Hevy.Mcp/Diagnostics/DiagnosticSnapshot.cs`
- Create: `src/Hevy.Mcp/Diagnostics/RedactingLoggerProvider.cs`
- Create: `src/Hevy.Mcp/Tools/DiagnosticTools.cs`
- Create: `tests/Hevy.Mcp.Tests/Diagnostics/DiagnosticSnapshotTests.cs`
- Create: `tests/Hevy.Mcp.Tests/Diagnostics/RedactingLoggerTests.cs`
- Create: `tests/Hevy.Transport.Tests/LiveReadSmokeTests.cs`
- Create: `tests/Hevy.Transport.Tests/LiveMutationSmokeTests.cs`

**Interfaces:**
- Consumes: Runtime metadata, safe operation events, and explicit environment gates.
- Produces: `get_diagnostics`, opt-in redacted stderr logs, and safely skipped live tests.

- [ ] **Step 1: Write failing diagnostics/redaction tests**

Feed log events containing a fake key, header, query string, workout title, timestamps, response body, and measurement. Assert none appears. Assert safe output includes version, runtime, transport, read-only state, operation category, duration bucket, status, correlation ID, and exception category.

- [ ] **Step 2: Implement allowlist-based diagnostics**

Build output from safe typed fields rather than regex-scrubbing arbitrary messages. Default log level `None` creates no provider and emits nothing.

- [ ] **Step 3: Write and implement live-test gates**

Read smoke tests require both `HEVY_LIVE_TESTS=true` and a non-empty existing `HEVY_API_KEY`. Mutation smoke tests additionally require `HEVY_LIVE_MUTATION_TESTS=true`. Missing gates produce a clear skipped test and make no request.

- [ ] **Step 4: Verify and commit**

Run all tests without secrets and confirm live tests skip. Search test output and repository content for fixture key patterns. Update ledger and commit with `feat: add privacy-safe diagnostics`.

---

### Task 9: Hardened Docker image and operator documentation

**Files:**
- Create: `Dockerfile`
- Create: `.dockerignore`
- Create: `README.md`
- Create: `SECURITY.md`
- Create: `CONTRIBUTING.md`
- Create: `tests/Hevy.Transport.Tests/ContainerSmokeTests.cs`

**Interfaces:**
- Consumes: Published `Hevy.Mcp` executable and environment contract.
- Produces: Non-root chiseled image and secure setup instructions for common MCP clients.

- [ ] **Step 1: Write a failing container smoke test/script**

The verification must inspect the built image configuration for a nonzero user, start stdio with `-i`, complete MCP initialization/tool listing, verify no host port, and start HTTP mode on loopback to test empty `/healthz` plus bearer rejection.

- [ ] **Step 2: Implement the multi-stage Dockerfile**

Use pinned .NET 10 SDK and `aspnet:10.0-noble-chiseled` runtime image digests, `dotnet restore --locked-mode`, `dotnet publish --no-restore`, `USER app`, read-only-friendly paths, no shell command entrypoint, OCI labels, and `ENTRYPOINT ["dotnet", "Hevy.Mcp.dll"]`.

- [ ] **Step 3: Write operator documentation**

Document threat model, environment variables, Docker build, stdio configurations for Codex/Claude Desktop/Cursor/VS Code/Gemini CLI/generic clients, HTTP reverse-proxy requirements, token rotation, read-only mode, dry runs, diagnostics, version pinning, and limitations. Never show a literal real-looking UUID key.

- [ ] **Step 4: Verify image and docs, then commit**

Run the Release build, all tests, Docker build, image inspection, and both transport smoke tests. Update ledger and commit with `docs: package and document hevy-client`.

---

### Task 10: CI, release provenance, and final acceptance verification

**Files:**
- Create: `.github/workflows/ci.yml`
- Create: `.github/workflows/release.yml`
- Create: `.github/dependabot.yml`
- Modify: `README.md`
- Modify: `docs/superpowers/progress/2026-07-26-hevy-client-progress.md`

**Interfaces:**
- Consumes: Entire repository and semantic Git tags.
- Produces: Reproducible CI, signed GHCR release workflow, SBOM/provenance, and final evidence ledger.

- [ ] **Step 1: Add CI workflow contract checks**

CI on pull requests and pushes must restore locked dependencies, check formatting, build Release with warnings as errors, run all non-live tests, validate the OpenAPI snapshot, build the Docker image, and run the container smoke test. Grant read-only permissions by default.

- [ ] **Step 2: Add release workflow**

On `v*.*.*` tags, validate semantic version, build `linux/amd64` and `linux/arm64`, push immutable version tags to GHCR, generate SPDX SBOM and GitHub provenance attestations, and keylessly sign the digest with Sigstore/Cosign. Grant only `contents:read`, `packages:write`, `id-token:write`, and `attestations:write`.

- [ ] **Step 3: Add dependency-update policy**

Configure weekly grouped NuGet, Docker, and GitHub Actions updates. Preview package majors require explicit maintainer review; the MCP 2.x preview must not be selected automatically.

- [ ] **Step 4: Run the full local acceptance suite**

Run, in order:

```bash
dotnet restore --locked-mode
dotnet format --verify-no-changes --no-restore
dotnet build -c Release --no-restore
dotnet test -c Release --no-build
docker build --pull -t hevy-client:test .
docker image inspect hevy-client:test
```

Then run stdio and HTTP container smoke tests. Expected: every command exits 0, no warnings or failed tests, live tests are skipped, the image user is non-root, and HTTP authentication tests pass.

- [ ] **Step 5: Audit requirements and secrets**

Map each design acceptance criterion to a passing test or inspection result in the ledger. Run repository searches for API-key patterns, telemetry packages/endpoints, non-Hevy runtime origins, placeholder markers, and accidentally tracked build artifacts. Record zero findings or fix them before continuing.

- [ ] **Step 6: Final review and commit**

Use the code-review and verification-before-completion skills. Resolve every correctness, security, or maintainability finding, rerun the complete acceptance suite, update the ledger with exact counts and image digest, and commit with `ci: automate verified releases`.

## Plan Self-Review Result

- Spec coverage: all ten design acceptance criteria map to Tasks 3 through 10; repository reproducibility and contract provenance map to Tasks 1 and 2.
- Placeholder scan: no deferred implementation markers or vague code steps remain.
- Type consistency: the `IHevyClient` read/write signatures are the shared boundary used by tool, cache, composite, and test tasks; naming is consistent across those consumers.
- Scope: the tasks form one dependency chain and each ends with an independently testable commit, so splitting into separate plans would duplicate contracts and verification.
