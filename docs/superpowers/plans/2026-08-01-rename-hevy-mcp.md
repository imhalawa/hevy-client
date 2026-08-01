# Rename hevy-mcp Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rename the published project, its GHCR image, and its internal .NET identity from `hevy-client` to `hevy-mcp`.

**Architecture:** `imhalawa/hevy-mcp` and `ghcr.io/imhalawa/hevy-mcp` become the current identities. `Hevy.Mcp.Client` and `HevyMcpClient` replace `Hevy.Client` and `HevyClient`. Old v0.1.0 coordinates stay only in historical verification records.

**Tech Stack:** GitHub, GHCR, .NET 10, Docker, GitHub Actions, xUnit.

## Global Constraints

- Preserve Central Package Management: versions stay only in `Directory.Packages.props`; all project `PackageReference` entries remain versionless.
- Preserve the published legacy digest `ghcr.io/imhalawa/hevy-client@sha256:f29625b6c0090af492e5115d186cb61583b5f903d79a5d6a73452e7c53188841` only as v0.1.0 historical evidence.
- Keep `HEVY_API_KEY` runtime-only and never add a secret to documentation, source, image layers, or command arguments.
- Use `apply_patch` for content changes and `git mv` only for explicit renames.

---

### Task 1: Rename public distribution identity

**Files:**
- Rename: `scripts/hevy-client-mcp` to `scripts/hevy-mcp`
- Modify: `.github/workflows/ci.yml`, `.github/workflows/release.yml`, `Dockerfile`, `README.md`, `CONTRIBUTING.md`, `SECURITY.md`, `scripts/Start-HevyClient.ps1`, `scripts/verify-reproducible-image.sh`
- Modify: `docs/release-checklist.md`, `docs/release-verification.md`, `tests/Hevy.Transport.Tests/DeliveryContractTests.cs`, `tests/Hevy.Transport.Tests/ContainerSmokeTests.cs`, `tests/Hevy.Transport.Tests/ReleaseSecurityContractTests.cs`

**Interfaces:**
- Produces: `imhalawa/hevy-mcp`, `ghcr.io/imhalawa/hevy-mcp`, and `scripts/hevy-mcp` for all current user and release paths.

- [ ] **Step 1: Make current-identity contracts fail**

Update delivery and container contracts so current Docker tags, OCI title, workflow solution command, and release image derivation require `hevy-mcp`. Keep the legacy digest, old GitHub repository, and old Cosign workflow identity asserted only in `docs/release-verification.md`.

- [ ] **Step 2: Verify the expected failure**

Run:

```sh
dotnet test tests/Hevy.Transport.Tests/Hevy.Transport.Tests.csproj --configuration Release --filter FullyQualifiedName~DeliveryContractTests|FullyQualifiedName~ContainerSmokeTests|FullyQualifiedName~ReleaseSecurityContractTests
```

Expected: FAIL because current artifacts still use `hevy-client`.

- [ ] **Step 3: Rename current public artifacts**

Run:

```sh
git mv scripts/hevy-client-mcp scripts/hevy-mcp
```

Patch all current public names to `hevy-mcp`: GitHub/GHCR paths, image tags,
container names, OCI title, SBOM artifact names, script defaults, workflow
commands, documentation, and current security/contributor text. Rename the
GitHub repository through GitHub and change `origin` to
`git@github.com:imhalawa/hevy-mcp.git`. Label old values in release verification
as **Legacy v0.1.0**; do not rewrite its digest or attestation identity.

- [ ] **Step 4: Verify and commit**

Run the Step 2 test command. Expected: PASS.

```sh
git add .github Dockerfile README.md CONTRIBUTING.md SECURITY.md scripts docs tests/Hevy.Transport.Tests
git commit -m "refactor: rename public project to hevy mcp"
```

### Task 2: Rename solution, project paths, namespaces, and public types

**Files:**
- Rename: `HevyClient.slnx` to `HevyMcp.slnx`
- Rename: `src/Hevy.Client` to `src/Hevy.Mcp.Client`
- Rename: `tests/Hevy.Client.Tests` to `tests/Hevy.Mcp.Client.Tests`
- Rename: `src/Hevy.Mcp.Client/Hevy.Client.csproj` to `src/Hevy.Mcp.Client/Hevy.Mcp.Client.csproj`
- Rename: `tests/Hevy.Mcp.Client.Tests/Hevy.Client.Tests.csproj` to `tests/Hevy.Mcp.Client.Tests/Hevy.Mcp.Client.Tests.csproj`
- Modify: solution/project references, every C# namespace/import/type use, Docker paths, workflow commands, tests, and documentation.

**Interfaces:**
- Produces: `Hevy.Mcp.Client`, `HevyMcpClient`, `IHevyMcpClient`, `HevyMcpClientOptions`, and `FakeHevyMcpClient`.

- [ ] **Step 1: Make renamed project references fail first**

Change the solution and project-reference paths to their target names before the
filesystem moves.

- [ ] **Step 2: Verify the expected failure**

Run:

```sh
dotnet build HevyMcp.slnx --configuration Release --no-restore
```

Expected: FAIL because renamed project paths and namespaces do not exist yet.

- [ ] **Step 3: Apply the literal source rename**

Use `git mv` for every path listed above. Patch all production and test files:
`Hevy.Client` becomes `Hevy.Mcp.Client`; `HevyClient*` becomes
`HevyMcpClient*`; `IHevyClient` becomes `IHevyMcpClient`; and
`FakeHevyClient` becomes `FakeHevyMcpClient`. Update solution paths, project
references, assembly identities, lock-file references, Dockerfile paths, CI,
and release commands.

- [ ] **Step 4: Prove CPM and renamed compilation**

Run:

```sh
rg -n '<PackageReference[^>]*Version=' --glob '*.csproj'
dotnet restore HevyMcp.slnx --locked-mode
dotnet format HevyMcp.slnx --verify-no-changes --no-restore
dotnet build HevyMcp.slnx --configuration Release --no-restore -warnaserror
dotnet test HevyMcp.slnx --configuration Release --no-build
```

Expected: `rg` has no output; locked restore, formatting, build, and all
non-live tests pass.

- [ ] **Step 5: Audit and commit**

Run:

```sh
rg -n -i 'hevy-client|HevyClient|Hevy\.Client' -g '!docs/release-verification.md' -g '!docs/release-checklist.md' -g '!docs/superpowers/**'
git diff --check
```

Expected: no current-name hits or whitespace errors.

```sh
git add -A
git commit -m "refactor: rename solution and client namespace"
```

### Task 3: Reconcile Dependabot after rename

**Files:**
- Verify: `Directory.Packages.props`, `.github/dependabot.yml`, all `.csproj`, Dependabot PRs #1–#3.

- [ ] **Step 1: Verify Central Package Management**

Run:

```sh
rg -n 'ManagePackageVersionsCentrally|CentralPackageTransitivePinningEnabled|<PackageVersion' Directory.Packages.props
rg -n '<PackageReference[^>]*Version=' --glob '*.csproj'
```

Expected: both CPM properties and all package versions are central; project
references have no `Version` attribute.

- [ ] **Step 2: Inspect Dependabot after repository rename**

Run:

```sh
gh pr list --repo imhalawa/hevy-mcp --state open --search 'author:app/dependabot'
```

Expected: Dependabot updates are rebased or recreated. Do not merge a PR with
failing CI.
