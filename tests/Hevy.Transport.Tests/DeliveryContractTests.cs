using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;
using Xunit;

namespace Hevy.Transport.Tests;

public sealed class DeliveryContractTests
{
  private static readonly string RepositoryRoot = DockerProcess.RepositoryRoot;

  [Fact]
  public void CiWorkflowIsParsedAndEnforcesTheCompleteSecretFreeAcceptanceSequence()
  {
    var workflow = Workflow("ci.yml");

    Assert.Equal(["pull_request", "push"], Keys(Map(workflow, "on")).Order(StringComparer.Ordinal).ToArray());
    AssertPermissions(workflow, ("contents", "read"));
    Assert.False(Map(workflow, "on").Children.ContainsKey(new YamlScalarNode("pull_request_target")));

    var runs = Steps(workflow)
        .Where(static step => step.Children.ContainsKey(new YamlScalarNode("run")))
        .Select(static step => Scalar(step, "run"))
        .ToArray();
    Assert.Contains("./scripts/run-actionlint.sh", runs);
    Assert.Contains("./scripts/validate-openapi.sh", runs);
    Assert.Contains("dotnet restore HevyClient.slnx --locked-mode", runs);
    Assert.Contains("dotnet format HevyClient.slnx --verify-no-changes --no-restore", runs);
    Assert.Contains("dotnet build HevyClient.slnx --configuration Release --no-restore -warnaserror", runs);
    Assert.Contains(runs, static run =>
        run.Contains("env -u HEVY_API_KEY -u HEVY_LIVE_TESTS -u HEVY_LIVE_MUTATION_TESTS -u MCP_AUTH_TOKEN", StringComparison.Ordinal) &&
        run.Contains("dotnet test HevyClient.slnx --configuration Release --no-build", StringComparison.Ordinal));
    Assert.Contains("docker build --pull --tag hevy-client:ci .", runs);
    Assert.Contains("docker image inspect hevy-client:ci", runs);
    Assert.Contains("./scripts/verify-reproducible-image.sh", runs);
    Assert.Contains(runs, static run => run.Contains("FullyQualifiedName~ContainerSmokeTests", StringComparison.Ordinal));
    Assert.DoesNotContain(runs, static run =>
        run.Contains("HEVY_LIVE_TESTS=true", StringComparison.Ordinal) ||
        run.Contains("HEVY_LIVE_MUTATION_TESTS=true", StringComparison.Ordinal) ||
        run.Contains("secrets.HEVY", StringComparison.Ordinal));
  }

  [Fact]
  public void ReleaseWorkflowPublishesOnlyAnExactVerifiedVersionAndDigest()
  {
    var workflow = Workflow("release.yml");

    var tagPatterns = Sequence(Map(Map(workflow, "on"), "push"), "tags")
        .Children.Cast<YamlScalarNode>().Select(static value => value.Value!).ToArray();
    Assert.Equal(["v*.*.*"], tagPatterns);
    AssertPermissions(
        workflow,
        ("attestations", "write"),
        ("contents", "read"),
        ("id-token", "write"),
        ("packages", "write"));
    Assert.Equal("release-ghcr-${{ github.repository }}", Scalar(Map(workflow, "concurrency"), "group"));
    Assert.Equal("false", Scalar(Map(workflow, "concurrency"), "cancel-in-progress"));

    var publishJob = Map(Map(workflow, "jobs"), "publish");
    Assert.Equal("release", Scalar(publishJob, "environment"));

    var steps = Steps(workflow).ToArray();
    var validate = Step(steps, "Validate release identity and security gate");
    Assert.Equal("release", Scalar(validate, "id"));
    Assert.Equal("./scripts/validate-release.sh", Scalar(validate, "run"));
    var validateEnvironment = Map(validate, "env");
    Assert.Equal("${{ vars.HEVY_CANONICAL_REPOSITORY }}", Scalar(validateEnvironment, "HEVY_CANONICAL_REPOSITORY"));
    Assert.Equal("${{ vars.HEVY_PRIVATE_ADVISORY_VERIFIED }}", Scalar(validateEnvironment, "HEVY_PRIVATE_ADVISORY_VERIFIED"));

    var reproducibility = Step(steps, "Verify reproducible multi-architecture image");
    Assert.Equal("reproducibility", Scalar(reproducibility, "id"));
    var reproducibilityEnvironment = Map(reproducibility, "env");
    Assert.Equal("${{ steps.release.outputs.revision }}", Scalar(reproducibilityEnvironment, "REVISION"));
    Assert.Equal("${{ steps.release.outputs.source }}", Scalar(reproducibilityEnvironment, "SOURCE_URL"));
    Assert.Equal("${{ steps.release.outputs.version }}", Scalar(reproducibilityEnvironment, "VERSION"));
    Assert.Equal("./scripts/verify-reproducible-image.sh", Scalar(reproducibility, "run"));

    var build = Step(steps, "Build and stage multi-architecture digest");
    Assert.Equal("build", Scalar(build, "id"));
    Assert.Equal(
        "${{ steps.reproducibility.outputs.source_date_epoch }}",
        Scalar(Map(build, "env"), "SOURCE_DATE_EPOCH"));
    var buildWith = Map(build, "with");
    Assert.Equal("linux/amd64,linux/arm64", Scalar(buildWith, "platforms"));
    Assert.Equal("${{ steps.release.outputs.image }}", Scalar(buildWith, "tags"));
    var outputs = Scalar(buildWith, "outputs");
    Assert.Contains("push-by-digest=true", outputs, StringComparison.Ordinal);
    Assert.Contains("name-canonical=true", outputs, StringComparison.Ordinal);
    Assert.Contains("push=true", outputs, StringComparison.Ordinal);
    Assert.Contains("rewrite-timestamp=true", outputs, StringComparison.Ordinal);
    Assert.Contains("compatibility-version=30", outputs, StringComparison.Ordinal);
    Assert.Contains("oci-mediatypes=true", outputs, StringComparison.Ordinal);
    Assert.DoesNotContain("steps.release.outputs.version", outputs, StringComparison.Ordinal);
    Assert.Equal("false", Scalar(buildWith, "sbom"));
    Assert.Equal("false", Scalar(buildWith, "provenance"));
    var buildArguments = Scalar(buildWith, "build-args");
    Assert.Contains("VERSION=${{ steps.release.outputs.version }}", buildArguments, StringComparison.Ordinal);
    Assert.Contains("REVISION=${{ steps.release.outputs.revision }}", buildArguments, StringComparison.Ordinal);
    Assert.Contains("SOURCE_URL=${{ steps.release.outputs.source }}", buildArguments, StringComparison.Ordinal);

    var imageVerification = Step(steps, "Verify published digest platforms labels and assembly version");
    Assert.Equal("image", Scalar(imageVerification, "id"));
    var imageVerificationEnvironment = Map(imageVerification, "env");
    Assert.Equal("${{ steps.reproducibility.outputs.index_digest }}", Scalar(imageVerificationEnvironment, "REPRO_INDEX_DIGEST"));
    Assert.Equal("${{ steps.reproducibility.outputs.amd64_digest }}", Scalar(imageVerificationEnvironment, "REPRO_AMD64_DIGEST"));
    Assert.Equal("${{ steps.reproducibility.outputs.arm64_digest }}", Scalar(imageVerificationEnvironment, "REPRO_ARM64_DIGEST"));
    var imageVerificationRun = Scalar(imageVerification, "run");
    Assert.Contains(
        "./scripts/capture-bounded-output.sh \"$index_file\" docker buildx imagetools inspect --raw \"$IMAGE@$IMAGE_DIGEST\"",
        imageVerificationRun,
        StringComparison.Ordinal);
    Assert.DoesNotContain(
        "imagetools inspect --raw \"$IMAGE@$IMAGE_DIGEST\" > \"$index_file\"",
        imageVerificationRun,
        StringComparison.Ordinal);
    Assert.Contains("./scripts/verify-staged-index.sh", imageVerificationRun, StringComparison.Ordinal);
    var amd64Attestation = Step(steps, "Attest amd64 container SBOM");
    var arm64Attestation = Step(steps, "Attest arm64 container SBOM");
    Assert.StartsWith("actions/attest-sbom@", Scalar(amd64Attestation, "uses"), StringComparison.Ordinal);
    Assert.StartsWith("actions/attest-sbom@", Scalar(arm64Attestation, "uses"), StringComparison.Ordinal);
    Assert.DoesNotContain(new YamlScalarNode("create-storage-record"), Map(amd64Attestation, "with").Children.Keys);
    Assert.DoesNotContain(new YamlScalarNode("create-storage-record"), Map(arm64Attestation, "with").Children.Keys);
    Assert.Equal("${{ steps.image.outputs.amd64_digest }}", Scalar(Map(amd64Attestation, "with"), "subject-digest"));
    Assert.Equal("${{ steps.image.outputs.arm64_digest }}", Scalar(Map(arm64Attestation, "with"), "subject-digest"));

    var installSyft = Step(steps, "Install pinned Syft");
    Assert.Equal("./scripts/install-syft.sh", Scalar(installSyft, "run"));
    var extractSboms = Step(steps, "Generate platform SPDX SBOMs");
    var extractSbomsRun = Scalar(extractSboms, "run");
    Assert.Contains("syft \"registry:$IMAGE@$AMD64_DIGEST\"", extractSbomsRun, StringComparison.Ordinal);
    Assert.Contains("syft \"registry:$IMAGE@$ARM64_DIGEST\"", extractSbomsRun, StringComparison.Ordinal);
    Assert.Contains("./scripts/validate-spdx.sh", extractSbomsRun, StringComparison.Ordinal);

    Assert.StartsWith(
        "actions/attest-build-provenance@",
        Scalar(Step(steps, "Attest staged container provenance"), "uses"),
        StringComparison.Ordinal);

    var tagCheck = Step(steps, "Authenticate GHCR version-tag lookup");
    Assert.Equal("${{ secrets.GITHUB_TOKEN }}", Scalar(Map(tagCheck, "env"), "GHCR_TOKEN"));
    Assert.Equal(
        "./scripts/ghcr-manifest.sh \"$IMAGE\" \"$RELEASE_VERSION\" >/dev/null",
        Scalar(tagCheck, "run"));

    var runs = steps
        .Where(static step => step.Children.ContainsKey(new YamlScalarNode("run")))
        .Select(static step => Scalar(step, "run"))
        .ToArray();
    Assert.All(runs, static run => Assert.DoesNotContain("${{", run, StringComparison.Ordinal));
    Assert.Contains(runs, static run => run.Contains("cosign sign --yes", StringComparison.Ordinal));
    Assert.Contains(runs, static run =>
        run.Contains("cosign verify", StringComparison.Ordinal) &&
        run.Contains("--certificate-github-workflow-sha \"$REVISION\"", StringComparison.Ordinal));
    Assert.Contains(runs, static run =>
        run.Contains("gh attestation verify", StringComparison.Ordinal) &&
        run.Contains("--bundle-from-oci", StringComparison.Ordinal) &&
        run.Contains("--predicate-type \"$SPDX_PREDICATE_TYPE\"", StringComparison.Ordinal));
    Assert.Contains(runs, static run => run.Contains("docker buildx imagetools inspect", StringComparison.Ordinal));
    Assert.Contains(runs, static run => run.Contains("coproc MCP_SERVER", StringComparison.Ordinal));
    Assert.DoesNotContain(Scalar(buildWith, "tags"), "latest", StringComparison.OrdinalIgnoreCase);

    var loginIndex = Array.IndexOf(steps, Step(steps, "Log in to GHCR"));
    Assert.True(Array.IndexOf(steps, Step(steps, "Run real release container smokes")) < loginIndex);
    Assert.True(Array.IndexOf(steps, Step(steps, "Verify reproducible multi-architecture image")) < loginIndex);
    Assert.DoesNotContain(steps, static step =>
        Scalar(step, "name") is "Build local release-check image" or "Inspect local release-check image");

    var promotion = Step(steps, "Promote verified digest to version tag");
    var promotionIndex = Array.IndexOf(steps, promotion);
    Assert.Equal(steps.Length - 1, promotionIndex);
    Assert.True(promotionIndex > Array.IndexOf(steps, Step(steps, "Verify GitHub attestations")));
    Assert.True(promotionIndex > Array.IndexOf(steps, Step(steps, "Keylessly sign and verify the staged digest")));
    var promotionRun = Scalar(promotion, "run");
    Assert.Equal(
        "exec ./scripts/promote-ghcr-tag.sh \"$IMAGE\" \"$RELEASE_VERSION\" \"$IMAGE_DIGEST\"",
        promotionRun);

    Assert.Contains(steps, static step =>
        step.Children.TryGetValue(new YamlScalarNode("uses"), out var uses) &&
        ((YamlScalarNode)uses).Value!.StartsWith("actions/attest-build-provenance@", StringComparison.Ordinal));
    Assert.Contains(steps, static step =>
        step.Children.TryGetValue(new YamlScalarNode("uses"), out var uses) &&
        ((YamlScalarNode)uses).Value!.StartsWith("sigstore/cosign-installer@", StringComparison.Ordinal));

    var digestConsumers = steps
        .Where(static step => step.Children.TryGetValue(new YamlScalarNode("env"), out _))
        .Select(static step => Map(step, "env"))
        .Where(static environment => environment.Children.ContainsKey(new YamlScalarNode("IMAGE_DIGEST")))
        .ToArray();
    Assert.NotEmpty(digestConsumers);
    Assert.All(digestConsumers, static environment =>
        Assert.Equal("${{ steps.build.outputs.digest }}", Scalar(environment, "IMAGE_DIGEST")));

    var readme = File.ReadAllText(Path.Combine(RepositoryRoot, "README.md"));
    Assert.Contains("--certificate-github-workflow-sha COMMIT_SHA", readme, StringComparison.Ordinal);
  }

  [Fact]
  public void ReleaseExecutionToolchainMatchesAuditedVersionAndManifestDigestPins()
  {
    using var document = JsonDocument.Parse(
        File.ReadAllText(Path.Combine(RepositoryRoot, ".github", "tools-lock.json")));
    var tools = document.RootElement.GetProperty("tools");
    var workflow = Workflow("release.yml");
    var steps = Steps(workflow).ToArray();

    var binfmt = tools.GetProperty("binfmt");
    var qemuWith = Map(Step(steps, "Set up QEMU"), "with");
    Assert.Equal(
        $"{binfmt.GetProperty("image").GetString()}@sha256:{binfmt.GetProperty("sha256").GetString()}",
        Scalar(qemuWith, "image"));

    var buildx = tools.GetProperty("buildx");
    var buildkit = tools.GetProperty("buildkit");
    var buildxWith = Map(Step(steps, "Set up Docker Buildx"), "with");
    Assert.DoesNotContain(new YamlScalarNode("version"), buildxWith.Children.Keys);
    Assert.Equal(
        $"image={buildkit.GetProperty("image").GetString()}@sha256:{buildkit.GetProperty("sha256").GetString()}",
        Scalar(buildxWith, "driver-opts"));

    var ciSteps = Steps(Workflow("ci.yml")).ToArray();
    Assert.Equal(
        $"{binfmt.GetProperty("image").GetString()}@sha256:{binfmt.GetProperty("sha256").GetString()}",
        Scalar(Map(Step(ciSteps, "Set up QEMU"), "with"), "image"));
    var ciBuildxWith = Map(Step(ciSteps, "Set up Docker Buildx"), "with");
    Assert.DoesNotContain(new YamlScalarNode("version"), ciBuildxWith.Children.Keys);
    Assert.Equal(
        $"image={buildkit.GetProperty("image").GetString()}@sha256:{buildkit.GetProperty("sha256").GetString()}",
        Scalar(ciBuildxWith, "driver-opts"));

    foreach (var tool in new[] { binfmt, buildkit })
    {
      Assert.Matches("^[0-9a-f]{64}$", tool.GetProperty("sha256").GetString()!);
      Assert.StartsWith("https://github.com/", tool.GetProperty("source").GetString(), StringComparison.Ordinal);
    }

    Assert.Matches("^[0-9a-f]{40}$", buildx.GetProperty("commit").GetString()!);
    Assert.Matches("^[0-9a-f]{64}$", buildx.GetProperty("sha256").GetString()!);
    Assert.Equal(
        $"buildx-v{buildx.GetProperty("version").GetString()}.linux-amd64",
        buildx.GetProperty("archive").GetString());
    Assert.Equal("./scripts/install-buildx.sh", Scalar(Step(steps, "Install pinned Buildx"), "run"));
    Assert.Equal("./scripts/install-buildx.sh", Scalar(Step(ciSteps, "Install pinned Buildx"), "run"));
    Assert.Equal("./scripts/verify-buildx-version.sh", Scalar(Step(steps, "Verify pinned Buildx"), "run"));
    Assert.Equal("./scripts/verify-buildx-version.sh", Scalar(Step(ciSteps, "Verify pinned Buildx"), "run"));
    Assert.True(Array.IndexOf(steps, Step(steps, "Install pinned Buildx")) < Array.IndexOf(steps, Step(steps, "Set up Docker Buildx")));
    Assert.True(Array.IndexOf(steps, Step(steps, "Set up Docker Buildx")) < Array.IndexOf(steps, Step(steps, "Verify pinned Buildx")));
    Assert.True(Array.IndexOf(ciSteps, Step(ciSteps, "Install pinned Buildx")) < Array.IndexOf(ciSteps, Step(ciSteps, "Set up Docker Buildx")));
    Assert.True(Array.IndexOf(ciSteps, Step(ciSteps, "Set up Docker Buildx")) < Array.IndexOf(ciSteps, Step(ciSteps, "Verify pinned Buildx")));

    var syft = tools.GetProperty("syft");
    Assert.Matches("^[0-9a-f]{64}$", syft.GetProperty("sha256").GetString()!);
    Assert.Matches("^[0-9a-f]{40}$", syft.GetProperty("commit").GetString()!);
    Assert.Equal(
        $"https://github.com/anchore/syft/releases/tag/v{syft.GetProperty("version").GetString()}",
        syft.GetProperty("source").GetString());
  }

  [Fact]
  public void EveryExternalActionUsesAnAuditedFullCommitPin()
  {
    var lockPath = Path.Combine(RepositoryRoot, ".github", "actions-lock.json");
    Assert.True(File.Exists(lockPath), "The audited action lock document must exist.");
    using var lockDocument = JsonDocument.Parse(File.ReadAllText(lockPath));
    var actions = lockDocument.RootElement.GetProperty("actions");

    var usesValues = new[] { Workflow("ci.yml"), Workflow("release.yml") }
        .SelectMany(Steps)
        .Where(static step => step.Children.ContainsKey(new YamlScalarNode("uses")))
        .Select(static step => Scalar(step, "uses"))
        .Where(static value => !value.StartsWith("./", StringComparison.Ordinal))
        .ToArray();

    Assert.NotEmpty(usesValues);
    foreach (var uses in usesValues)
    {
      var match = Regex.Match(uses, "^(?<action>[^@]+)@(?<commit>[0-9a-f]{40})$", RegexOptions.CultureInvariant);
      Assert.True(match.Success, $"Action reference is not pinned by a full commit: {uses}");
      var action = match.Groups["action"].Value;
      var locked = actions.GetProperty(action);
      Assert.Equal(match.Groups["commit"].Value, locked.GetProperty("commit").GetString());
      var version = locked.GetProperty("version").GetString();
      Assert.Matches("^v[0-9]+(?:\\.[0-9]+){1,2}$", version!);
      Assert.Equal($"https://github.com/{action}/tree/{version}", locked.GetProperty("source").GetString());
    }
  }

  [Fact]
  public void ActionlintLockRecordsAnAuditedReleaseChecksum()
  {
    var lockPath = Path.Combine(RepositoryRoot, ".github", "tools-lock.json");
    Assert.True(File.Exists(lockPath), "The audited CI-tool lock document must exist.");
    using var document = JsonDocument.Parse(File.ReadAllText(lockPath));
    var actionlint = document.RootElement.GetProperty("tools").GetProperty("actionlint");
    var version = actionlint.GetProperty("version").GetString()!;
    var checksum = actionlint.GetProperty("sha256").GetString()!;
    Assert.Matches("^[0-9a-f]{64}$", checksum);
    Assert.Equal($"https://github.com/rhysd/actionlint/releases/tag/v{version}", actionlint.GetProperty("source").GetString());
  }

  [Fact]
  public async Task WorkflowSupportScriptsAreCommittedAsExecutables()
  {
    var scripts = new[]
    {
      "scripts/audit-repository.sh",
      "scripts/capture-bounded-output.sh",
      "scripts/ghcr-manifest.sh",
      "scripts/install-buildx.sh",
      "scripts/install-syft.sh",
      "scripts/promote-ghcr-tag.sh",
      "scripts/run-actionlint.sh",
      "scripts/validate-openapi.sh",
      "scripts/validate-oci-index.sh",
      "scripts/validate-release.sh",
      "scripts/validate-sha256-digest.sh",
      "scripts/validate-spdx.sh",
      "scripts/verify-reproducible-image.sh",
      "scripts/verify-buildx-version.sh",
      "scripts/verify-staged-index.sh",
    };
    var result = await RunProcessAsync(
        RepositoryRoot,
        "git",
        ["ls-files", "--stage", "--", .. scripts]);

    Assert.Equal(0, result.ExitCode);
    var entries = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(scripts.Length, entries.Length);
    Assert.All(entries, static entry => Assert.StartsWith("100755 ", entry, StringComparison.Ordinal));
  }

  [Fact]
  public void RepositoryFormattingPolicyDocumentsDeliveryFileIndentation()
  {
    var editorConfig = File.ReadAllText(Path.Combine(RepositoryRoot, ".editorconfig"));

    Assert.Contains("[*.{json,sh,yaml,yml}]", editorConfig, StringComparison.Ordinal);
    Assert.Matches("(?m)^indent_size = 2$", editorConfig[(editorConfig.IndexOf("[*.{json,sh,yaml,yml}]", StringComparison.Ordinal))..]);
  }

  [Fact]
  public void DependabotGroupsWeeklyStableUpdatesAndCannotSelectMcpTwo()
  {
    var dependabot = Yaml(Path.Combine(RepositoryRoot, ".github", "dependabot.yml"));
    Assert.Equal("2", Scalar(dependabot, "version"));
    var updates = Sequence(dependabot, "updates").Children.Cast<YamlMappingNode>().ToArray();
    Assert.Equal(["docker", "github-actions", "nuget"], updates.Select(static update => Scalar(update, "package-ecosystem")).Order(StringComparer.Ordinal).ToArray());

    foreach (var update in updates)
    {
      Assert.Equal("/", Scalar(update, "directory"));
      Assert.Equal("weekly", Scalar(Map(update, "schedule"), "interval"));
      var stable = Map(Map(update, "groups"), "stable-updates");
      Assert.Equal(["minor", "patch"], Sequence(stable, "update-types").Children.Cast<YamlScalarNode>().Select(static value => value.Value!).Order(StringComparer.Ordinal).ToArray());
    }

    var nuget = updates.Single(static update => Scalar(update, "package-ecosystem") == "nuget");
    var ignored = Sequence(nuget, "ignore").Children.Cast<YamlMappingNode>().ToArray();
    Assert.Equal(
        ["ModelContextProtocol", "ModelContextProtocol.AspNetCore"],
        ignored.Select(static item => Scalar(item, "dependency-name")).Order(StringComparer.Ordinal).ToArray());
    Assert.All(ignored, static item =>
    {
      Assert.Equal(
          ["2.*"],
          Sequence(item, "versions").Children.Cast<YamlScalarNode>().Select(static value => value.Value!).ToArray());
      Assert.Contains("version-update:semver-major", Sequence(item, "update-types").Children.Cast<YamlScalarNode>().Select(static value => value.Value));
    });

    var packages = File.ReadAllText(Path.Combine(RepositoryRoot, "Directory.Packages.props"));
    Assert.Contains("<PackageVersion Include=\"ModelContextProtocol\" Version=\"1.4.1\" />", packages, StringComparison.Ordinal);
    Assert.Contains("<PackageVersion Include=\"ModelContextProtocol.AspNetCore\" Version=\"1.4.1\" />", packages, StringComparison.Ordinal);
    Assert.DoesNotContain("preview", packages, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public async Task ReleaseValidatorAcceptsLightweightAndAnnotatedExactTagsAndRejectsUnsafeIdentity()
  {
    var script = Path.Combine(RepositoryRoot, "scripts", "validate-release.sh");
    Assert.True(File.Exists(script), "The executable release validator must exist.");

    await using var repository = await TemporaryGitRepository.CreateAsync();
    foreach (var annotated in new[] { false, true })
    {
      var tag = annotated ? "v2.3.4" : "v1.2.3";
      await repository.TagAsync(tag, annotated);
      var result = await repository.RunValidatorAsync(script, tag, securityVerified: "true");
      Assert.Equal(0, result.ExitCode);
      Assert.Contains($"version={tag[1..]}", result.OutputFile, StringComparison.Ordinal);
      Assert.Contains("image=ghcr.io/example/hevy-client", result.OutputFile, StringComparison.Ordinal);
      Assert.Contains($"revision={repository.Commit}", result.OutputFile, StringComparison.Ordinal);
      Assert.Contains("source=https://github.com/Example/Hevy-Client", result.OutputFile, StringComparison.Ordinal);
    }

    foreach (var invalidTag in new[] { "v1.2", "v1.2.3-rc.1", "v01.2.3", "1.2.3" })
    {
      var result = await repository.RunValidatorAsync(script, invalidTag, securityVerified: "true", createTag: false);
      Assert.NotEqual(0, result.ExitCode);
    }

    var blocked = await repository.RunValidatorAsync(script, "v1.2.3", securityVerified: "false");
    Assert.NotEqual(0, blocked.ExitCode);
    Assert.Contains("private vulnerability reporting", blocked.StandardError, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public async Task RepositoryAuditAcceptsThisTreeAndRejectsSecretsTelemetryForeignOriginsPlaceholdersAndArtifacts()
  {
    var script = Path.Combine(RepositoryRoot, "scripts", "audit-repository.sh");
    Assert.True(File.Exists(script), "The repeatable release audit must exist.");

    var current = await RunProcessAsync(RepositoryRoot, "/bin/sh", script, RepositoryRoot);
    Assert.Equal(0, current.ExitCode);

    var fixture = Path.Combine(Path.GetTempPath(), $"hevy-audit-{Guid.NewGuid():N}");
    Directory.CreateDirectory(Path.Combine(fixture, "src"));
    Directory.CreateDirectory(Path.Combine(fixture, "docs"));
    Directory.CreateDirectory(Path.Combine(fixture, "tests"));
    Directory.CreateDirectory(Path.Combine(fixture, "bin"));
    try
    {
      await GitAsync(fixture, "init", "--quiet");
      var syntheticSecret = string.Concat("hvy_live_7Qm4", "N2x9Vp6K8s3R5t1W");
      var placeholder = string.Concat("TO", "DO");
      var telemetryNamespace = string.Concat("Open", "Telemetry");
      var foreignOrigin = string.Concat("https://telemetry.", "example.test");
      await File.WriteAllTextAsync(
          Path.Combine(fixture, "src", "Unsafe.cs"),
          $"// {placeholder} placeholder\n// {foreignOrigin}\nusing {telemetryNamespace};\n");
      await File.WriteAllTextAsync(
          Path.Combine(fixture, "docs", "leak.json"),
          $"{{\"HEVY_API_KEY\":\"{syntheticSecret}\"}}\n");
      await File.WriteAllTextAsync(
          Path.Combine(fixture, "tests", "leak.txt"),
          $"HEVY_API_KEY={syntheticSecret}\n");
      await File.WriteAllTextAsync(
          Path.Combine(fixture, "settings.toml"),
          $"mcp_auth_token = \"{syntheticSecret}\"\n");
      await File.WriteAllTextAsync(Path.Combine(fixture, ".env"), $"HEVY_API_KEY={syntheticSecret}\n");
      await File.WriteAllBytesAsync(Path.Combine(fixture, "bin", "Unsafe.dll"), [1, 2, 3]);
      await GitAsync(fixture, "add", ".");

      var unsafeResult = await RunProcessAsync(fixture, "/bin/sh", script, fixture);
      Assert.NotEqual(0, unsafeResult.ExitCode);
      Assert.Contains("secret", unsafeResult.StandardError, StringComparison.OrdinalIgnoreCase);
      Assert.Contains("credential assignment", unsafeResult.StandardError, StringComparison.OrdinalIgnoreCase);
      Assert.Contains("telemetry", unsafeResult.StandardError, StringComparison.OrdinalIgnoreCase);
      Assert.Contains("origin", unsafeResult.StandardError, StringComparison.OrdinalIgnoreCase);
      Assert.Contains("placeholder", unsafeResult.StandardError, StringComparison.OrdinalIgnoreCase);
      Assert.Contains("artifact", unsafeResult.StandardError, StringComparison.OrdinalIgnoreCase);
    }
    finally
    {
      Directory.Delete(fixture, recursive: true);
    }
  }

  [Theory]
  [InlineData("docs/leak.yml", "HEVY_API_KEY: {0}\n", "credential assignment")]
  [InlineData("tests/mixed.txt", "api_key = \"inventory-test-api-key\"; MCP_AUTH_TOKEN: {0}\n", "credential assignment")]
  [InlineData("docs/embedded.yml", "HEVY_API_KEY: \"AAAAAAAA{1}BBBBBBBB\"\n", "credential assignment")]
  [InlineData("docs/dotted.yml", "MCP_AUTH_TOKEN: {0}.signed.segment~suffix\n", "credential assignment")]
  [InlineData("docs/tilde.yml", "MCP_AUTH_TOKEN: {0}~agent-token\n", "credential assignment")]
  [InlineData("src/Origin.cs", "// https://api.hevyapp.com.evil/v1\n", "origin")]
  public async Task RepositoryAuditRejectsAdversarialCredentialsAndLookalikeOrigins(
      string relativePath,
      string contents,
      string expectedFinding)
  {
    var script = Path.Combine(RepositoryRoot, "scripts", "audit-repository.sh");
    var fixture = Path.Combine(Path.GetTempPath(), $"hevy-audit-adversarial-{Guid.NewGuid():N}");
    Directory.CreateDirectory(fixture);
    try
    {
      await GitAsync(fixture, "init", "--quiet");
      var path = Path.Combine(fixture, relativePath);
      Directory.CreateDirectory(Path.GetDirectoryName(path)!);
      var syntheticSecret = string.Concat("hvy_live_adversarial_", "7Qm4N2x9Vp6K8s3R5t1W");
      await File.WriteAllTextAsync(path, string.Format(contents, syntheticSecret, "inventory-test-api-key"));
      await GitAsync(fixture, "add", ".");

      var result = await RunProcessAsync(fixture, "/bin/sh", script, fixture);

      Assert.NotEqual(0, result.ExitCode);
      Assert.Contains(expectedFinding, result.StandardError, StringComparison.OrdinalIgnoreCase);
    }
    finally
    {
      Directory.Delete(fixture, recursive: true);
    }
  }

  private static YamlMappingNode Workflow(string fileName) =>
      Yaml(Path.Combine(RepositoryRoot, ".github", "workflows", fileName));

  private static YamlMappingNode Yaml(string path)
  {
    Assert.True(File.Exists(path), $"Required parsed YAML file does not exist: {path}");
    var stream = new YamlStream();
    using var reader = File.OpenText(path);
    stream.Load(reader);
    Assert.Single(stream.Documents);
    return Assert.IsType<YamlMappingNode>(stream.Documents[0].RootNode);
  }

  private static IEnumerable<YamlMappingNode> Steps(YamlMappingNode workflow) =>
      Map(workflow, "jobs").Children.Values
          .Cast<YamlMappingNode>()
          .SelectMany(static job => Sequence(job, "steps").Children.Cast<YamlMappingNode>());

  private static YamlMappingNode Step(IEnumerable<YamlMappingNode> steps, string name) =>
      steps.Single(step => Scalar(step, "name") == name);

  private static void AssertPermissions(YamlMappingNode workflow, params (string Name, string Access)[] expected)
  {
    var actual = Map(workflow, "permissions").Children
        .ToDictionary(static item => ((YamlScalarNode)item.Key).Value!, static item => ((YamlScalarNode)item.Value).Value!, StringComparer.Ordinal);
    var expectedValues = expected
        .OrderBy(static item => item.Name, StringComparer.Ordinal)
        .Select(static item => $"{item.Name}:{item.Access}")
        .ToArray();
    var actualValues = actual
        .OrderBy(static item => item.Key, StringComparer.Ordinal)
        .Select(static item => $"{item.Key}:{item.Value}")
        .ToArray();
    Assert.Equal(expectedValues, actualValues);
  }

  private static IEnumerable<string> Keys(YamlMappingNode mapping) =>
      mapping.Children.Keys.Cast<YamlScalarNode>().Select(static key => key.Value!);

  private static YamlMappingNode Map(YamlMappingNode parent, string key) =>
      Assert.IsType<YamlMappingNode>(parent.Children[new YamlScalarNode(key)]);

  private static YamlSequenceNode Sequence(YamlMappingNode parent, string key) =>
      Assert.IsType<YamlSequenceNode>(parent.Children[new YamlScalarNode(key)]);

  private static string Scalar(YamlMappingNode parent, string key) =>
      Assert.IsType<YamlScalarNode>(parent.Children[new YamlScalarNode(key)]).Value!;

  internal static Task<ProcessResult> RunProcessAsync(
      string workingDirectory,
      string executable,
      params string[] arguments) =>
      RunProcessAsync(workingDirectory, executable, environment: null, arguments);

  internal static async Task<ProcessResult> RunProcessAsync(
      string workingDirectory,
      string executable,
      IReadOnlyDictionary<string, string?>? environment,
      params string[] arguments)
  {
    var startInfo = new ProcessStartInfo
    {
      FileName = executable,
      WorkingDirectory = workingDirectory,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
    };
    foreach (var argument in arguments)
    {
      startInfo.ArgumentList.Add(argument);
    }

    if (environment is not null)
    {
      foreach (var item in environment)
      {
        startInfo.Environment[item.Key] = item.Value;
      }
    }

    using var process = Process.Start(startInfo)!;
    var standardOutput = await process.StandardOutput.ReadToEndAsync();
    var standardError = await process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    return new ProcessResult(standardOutput, standardError, process.ExitCode);
  }

  private static async Task GitAsync(string workingDirectory, params string[] arguments)
  {
    var result = await RunProcessAsync(workingDirectory, "git", arguments);
    Assert.Equal(0, result.ExitCode);
  }

  private sealed class TemporaryGitRepository : IAsyncDisposable
  {
    private readonly string path;

    private TemporaryGitRepository(string path, string commit)
    {
      this.path = path;
      Commit = commit;
    }

    public string Commit { get; }

    public static async Task<TemporaryGitRepository> CreateAsync()
    {
      var path = Path.Combine(Path.GetTempPath(), $"hevy-release-{Guid.NewGuid():N}");
      Directory.CreateDirectory(path);
      await TemporaryGitRepository.GitAsync(path, "init", "--quiet");
      await TemporaryGitRepository.GitAsync(path, "config", "user.name", "Release Test");
      await TemporaryGitRepository.GitAsync(path, "config", "user.email", "release@example.invalid");
      await File.WriteAllTextAsync(Path.Combine(path, "source.txt"), "fixture");
      await GitAsync(path, "add", "source.txt");
      await GitAsync(path, "commit", "--quiet", "-m", "fixture");
      var commit = (await GitAsync(path, "rev-parse", "HEAD")).StandardOutput.Trim();
      return new TemporaryGitRepository(path, commit);
    }

    public Task TagAsync(string tag, bool annotated) => annotated
        ? GitAsync(path, "tag", "-a", tag, "-m", "fixture tag")
        : GitAsync(path, "tag", tag);

    public async Task<ValidatorResult> RunValidatorAsync(
        string script,
        string tag,
        string securityVerified,
        bool createTag = true)
    {
      if (createTag && !await TagExistsAsync(tag))
      {
        await TagAsync(tag, annotated: false);
      }

      var output = Path.Combine(path, $"output-{Guid.NewGuid():N}.txt");
      var startInfo = new ProcessStartInfo
      {
        FileName = "/bin/sh",
        WorkingDirectory = path,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
      };
      startInfo.ArgumentList.Add(script);
      startInfo.Environment["GITHUB_REF_TYPE"] = "tag";
      startInfo.Environment["GITHUB_REF_NAME"] = tag;
      startInfo.Environment["GITHUB_SHA"] = Commit;
      startInfo.Environment["GITHUB_REPOSITORY"] = "Example/Hevy-Client";
      startInfo.Environment["GITHUB_SERVER_URL"] = "https://github.com";
      startInfo.Environment["HEVY_CANONICAL_REPOSITORY"] = "Example/Hevy-Client";
      startInfo.Environment["HEVY_PRIVATE_ADVISORY_VERIFIED"] = securityVerified;
      startInfo.Environment["GITHUB_OUTPUT"] = output;
      using var process = Process.Start(startInfo)!;
      var standardOutput = await process.StandardOutput.ReadToEndAsync();
      var standardError = await process.StandardError.ReadToEndAsync();
      await process.WaitForExitAsync();
      return new ValidatorResult(
          process.ExitCode,
          standardOutput,
          standardError,
          File.Exists(output) ? await File.ReadAllTextAsync(output) : string.Empty);
    }

    private async Task<bool> TagExistsAsync(string tag) =>
        (await GitAsync(path, "tag", "--list", tag)).StandardOutput.Trim() == tag;

    public ValueTask DisposeAsync()
    {
      Directory.Delete(path, recursive: true);
      return ValueTask.CompletedTask;
    }

    private static async Task<ProcessResult> GitAsync(string workingDirectory, params string[] arguments)
    {
      var result = await DeliveryContractTests.RunProcessAsync(workingDirectory, "git", arguments);
      Assert.Equal(0, result.ExitCode);
      return result;
    }
  }

  internal sealed record ProcessResult(string StandardOutput, string StandardError, int ExitCode = 0);

  private sealed record ValidatorResult(
      int ExitCode,
      string StandardOutput,
      string StandardError,
      string OutputFile);
}
