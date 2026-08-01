# Simplify README Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the release-audit-heavy README with a conventional AI/MCP project guide that explains Hevy, enables a safe first run of the public image, and links to focused reference material.

**Architecture:** `README.md` becomes the first-successful-use path: purpose, capabilities, published-image quick start, MCP connection, and concise operational boundaries. `docs/release-verification.md` owns the immutable-digest, Cosign, and GitHub-attestation instructions that do not belong in onboarding. The existing delivery contract test moves its release-verification assertion to that focused document and adds a durable first-run contract for the README.

**Tech Stack:** Markdown, Docker, PowerShell, xUnit 2.9.3, FluentAssertions.

## Global Constraints

- Keep `HEVY_API_KEY` exclusively runtime-supplied; never add a literal API key, bearer token, image-layer secret, command-line secret value, or committed environment file.
- The public v0.1.0 image reference is `ghcr.io/imhalawa/hevy-client@sha256:f29625b6c0090af492e5115d186cb61583b5f903d79a5d6a73452e7c53188841`.
- Preserve hardened stdio container arguments: `--rm`, `-i`, `--read-only`, `--tmpfs /tmp:rw,noexec,nosuid,size=16m`, and `-e HEVY_API_KEY`; do not publish a port for stdio.
- Keep HTTP documentation single-tenant, loopback-bound, protected by a distinct `MCP_AUTH_TOKEN`, and behind TLS termination.
- Keep all release-verification facts source-faithful: release v0.1.0, source commit `9ec3223c6bfe72d57435a50c8d0f19eb92d0624e`, release workflow path, Cosign identity, and GitHub attestation command.
- Apply the `tech-voice` contract: lead with the user action, retain decision-changing boundaries, and remove release-pipeline implementation detail from the README.

---

### Task 1: Move the release-verification contract to a focused document

**Files:**
- Create: `docs/release-verification.md`
- Modify: `tests/Hevy.Transport.Tests/DeliveryContractTests.cs:183-184`

**Interfaces:**
- Consumes: the v0.1.0 image digest, Cosign identity, workflow source commit, and `gh attestation verify` arguments currently in `README.md`.
- Produces: `docs/release-verification.md`, the single durable location for manual v0.1.0 image verification.

- [ ] **Step 1: Change the delivery contract to require the new verification document**

Replace the current README-only assertion with this focused contract immediately after the release-workflow assertions:

```csharp
var releaseVerification = File.ReadAllText(Path.Combine(RepositoryRoot, "docs", "release-verification.md"));
(releaseVerification).Should().MatchRegex("--certificate-github-workflow-sha [0-9a-f]{40}");
(releaseVerification).Should().Contain("gh attestation verify oci://ghcr.io/imhalawa/hevy-client@sha256:");
```

- [ ] **Step 2: Run the focused test to verify it fails**

Run:

```sh
dotnet test tests/Hevy.Transport.Tests/Hevy.Transport.Tests.csproj --configuration Release --filter FullyQualifiedName~DeliveryContractTests.ReleaseWorkflowPublishesOnlyAnExactVerifiedVersionAndDigest
```

Expected: FAIL because `docs/release-verification.md` does not exist.

- [ ] **Step 3: Create the focused release-verification document**

Create `docs/release-verification.md` with these exact sections:

~~~~markdown
# Release verification

## v0.1.0

Pull and run the immutable image reference:

```sh
docker pull ghcr.io/imhalawa/hevy-client@sha256:f29625b6c0090af492e5115d186cb61583b5f903d79a5d6a73452e7c53188841
```

Verify its keyless signature:

```sh
cosign verify ghcr.io/imhalawa/hevy-client@sha256:f29625b6c0090af492e5115d186cb61583b5f903d79a5d6a73452e7c53188841 \\
  --certificate-identity https://github.com/imhalawa/hevy-client/.github/workflows/release.yml@refs/tags/v0.1.0 \\
  --certificate-github-workflow-sha 9ec3223c6bfe72d57435a50c8d0f19eb92d0624e \\
  --certificate-oidc-issuer https://token.actions.githubusercontent.com
```

Verify the GitHub provenance and SBOM attestations:

```sh
gh attestation verify oci://ghcr.io/imhalawa/hevy-client@sha256:f29625b6c0090af492e5115d186cb61583b5f903d79a5d6a73452e7c53188841 \\
  --repo imhalawa/hevy-client \\
  --signer-workflow imhalawa/hevy-client/.github/workflows/release.yml \\
  --source-digest 9ec3223c6bfe72d57435a50c8d0f19eb92d0624e \\
  --source-ref refs/tags/v0.1.0 \\
  --bundle-from-oci
```
~~~~

Add one concise paragraph saying that the digest identifies immutable image
content while the version tag is only a convenience reference. Link to
`docs/release-checklist.md` for the maintainer release gate.

- [ ] **Step 4: Run the focused test to verify it passes**

Run:

```sh
dotnet test tests/Hevy.Transport.Tests/Hevy.Transport.Tests.csproj --configuration Release --filter FullyQualifiedName~DeliveryContractTests.ReleaseWorkflowPublishesOnlyAnExactVerifiedVersionAndDigest
```

Expected: PASS.

- [ ] **Step 5: Commit the release-verification extraction**

```sh
git add docs/release-verification.md tests/Hevy.Transport.Tests/DeliveryContractTests.cs
git commit -m "docs: extract release verification guide"
```

### Task 2: Establish the README quick-start contract

**Files:**
- Modify: `tests/Hevy.Transport.Tests/DeliveryContractTests.cs`

**Interfaces:**
- Consumes: the public v0.1.0 digest and the hardened stdio argument sequence.
- Produces: `ReadmeProvidesSafePublishedImageQuickStart`, which protects the reader's first successful-use path.

- [ ] **Step 1: Add a failing README quick-start contract**

Add this fact below the release-verification assertions:

```csharp
[Fact]
public void ReadmeProvidesSafePublishedImageQuickStart()
{
  var readme = File.ReadAllText(Path.Combine(RepositoryRoot, "README.md"));

  (readme).Should().Contain("## Quick start");
  (readme).Should().Contain("docker pull ghcr.io/imhalawa/hevy-client@sha256:f29625b6c0090af492e5115d186cb61583b5f903d79a5d6a73452e7c53188841");
  (readme).Should().Contain("docker run --rm -i --read-only");
  (readme).Should().Contain("--tmpfs /tmp:rw,noexec,nosuid,size=16m");
  (readme).Should().Contain("-e HEVY_API_KEY");
  (readme).Should().Contain("The image contains no API key.");
  (readme).Should().Contain("### Windows PowerShell");
  (readme).Should().Contain("[Release verification](docs/release-verification.md)");
}
```

- [ ] **Step 2: Run the new contract to verify it fails**

Run:

```sh
dotnet test tests/Hevy.Transport.Tests/Hevy.Transport.Tests.csproj --configuration Release --filter FullyQualifiedName~DeliveryContractTests.ReadmeProvidesSafePublishedImageQuickStart
```

Expected: FAIL because the existing README has no `## Quick start` section or
published-image pull command.

- [ ] **Step 3: Rewrite `README.md` around first successful use**

Replace the current 338-line README with this exact heading structure:

```markdown
# hevy-client

## What it is
## What it enables
## Quick start
### Windows PowerShell
## Connect an MCP client
## Safe operation
## Optional HTTP mode
## Configuration
## Project
```

Use the following portable quick-start command blocks verbatim:

```sh
docker pull ghcr.io/imhalawa/hevy-client@sha256:f29625b6c0090af492e5115d186cb61583b5f903d79a5d6a73452e7c53188841

read -r -s -p "Hevy API key: " HEVY_API_KEY && export HEVY_API_KEY && printf '\n'

docker run --rm -i --read-only --tmpfs /tmp:rw,noexec,nosuid,size=16m \\
  -e HEVY_API_KEY \\
  ghcr.io/imhalawa/hevy-client@sha256:f29625b6c0090af492e5115d186cb61583b5f903d79a5d6a73452e7c53188841
```

In `### Windows PowerShell`, retain the secure prompt flow and run the same
image/arguments. State that the program waiting without output is expected:
the server is waiting for MCP JSON-RPC on standard input.

Use one Codex registration command under `## Connect an MCP client`:

```sh
codex mcp add hevy -- docker run --rm -i --read-only --tmpfs /tmp:rw,noexec,nosuid,size=16m -e HEVY_API_KEY ghcr.io/imhalawa/hevy-client@sha256:f29625b6c0090af492e5115d186cb61583b5f903d79a5d6a73452e7c53188841
```

State that other stdio MCP clients use the same Docker command and that
graphical clients must acquire the key before they start the container; link to
`scripts/hevy-client-mcp` and `scripts/Start-HevyClient.ps1` rather than
embedding platform tutorials.

Keep `## Safe operation` to one compact list: the image contains no API key;
never place credentials in source, image layers, arguments, URLs, or committed
environment files; `HEVY_READ_ONLY=true` hides mutation tools; mutations accept
`dry_run: true`; there is no telemetry or persistent fitness-data store.

Keep `## Optional HTTP mode` to the existing hardened Docker command plus the
single-tenant, distinct-token, loopback, and TLS conditions. Retain the compact
configuration table with every existing variable and its exact accepted values.

Under `## Project`, link to `CONTRIBUTING.md`, `SECURITY.md`, `LICENSE`,
`docs/release-verification.md`, and `docs/release-checklist.md`. Remove the
full release-pipeline narrative, verification commands, base-image maintenance
instructions, individual desktop-client JSON blocks, and development build
commands from the README.

- [ ] **Step 4: Run the README contract to verify it passes**

Run:

```sh
dotnet test tests/Hevy.Transport.Tests/Hevy.Transport.Tests.csproj --configuration Release --filter FullyQualifiedName~DeliveryContractTests.ReadmeProvidesSafePublishedImageQuickStart
```

Expected: PASS.

- [ ] **Step 5: Run documentation and delivery verification**

Run:

```sh
dotnet test tests/Hevy.Transport.Tests/Hevy.Transport.Tests.csproj --configuration Release --no-restore
git diff --check
```

Expected: all transport tests pass and `git diff --check` reports no whitespace
errors.

- [ ] **Step 6: Commit the simplified README**

```sh
git add README.md tests/Hevy.Transport.Tests/DeliveryContractTests.cs
git commit -m "docs: simplify published image setup"
```
