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

    (Keys(Map(workflow, "on")).Order(StringComparer.Ordinal).ToArray()).Should().Equal(["pull_request", "push"]);
    AssertPermissions(workflow, ("contents", "read"));
    (Map(workflow, "on").Children.ContainsKey(new YamlScalarNode("pull_request_target"))).Should().BeFalse();

    var runs = Steps(workflow)
        .Where(static step => step.Children.ContainsKey(new YamlScalarNode("run")))
        .Select(static step => Scalar(step, "run"))
        .ToArray();
    (runs).Should().Contain("./scripts/run-actionlint.sh");
    (runs).Should().Contain("./scripts/validate-openapi.sh");
    (runs).Should().Contain("dotnet restore HevyClient.slnx --locked-mode");
    (runs).Should().Contain("dotnet format HevyClient.slnx --verify-no-changes --no-restore");
    (runs).Should().Contain("dotnet build HevyClient.slnx --configuration Release --no-restore -warnaserror");
    (runs).Should().Contain((static run =>
        run.Contains("env -u HEVY_API_KEY -u HEVY_LIVE_TESTS -u HEVY_LIVE_MUTATION_TESTS -u MCP_AUTH_TOKEN", StringComparison.Ordinal) &&
        run.Contains("dotnet test HevyClient.slnx --configuration Release --no-build", StringComparison.Ordinal)));
    (runs).Should().Contain("docker build --pull --tag hevy-client:ci .");
    (runs).Should().Contain("docker image inspect hevy-client:ci");
    (runs).Should().Contain("./scripts/verify-reproducible-image.sh");
    (runs).Should().Contain((static run => run.Contains("FullyQualifiedName~ContainerSmokeTests", StringComparison.Ordinal)));
    (runs).Should().NotContain((static run =>
        run.Contains("HEVY_LIVE_TESTS=true", StringComparison.Ordinal) ||
        run.Contains("HEVY_LIVE_MUTATION_TESTS=true", StringComparison.Ordinal) ||
        run.Contains("secrets.HEVY", StringComparison.Ordinal)));
  }

  [Fact]
  public void ReleaseWorkflowPublishesOnlyAnExactVerifiedVersionAndDigest()
  {
    var workflow = Workflow("release.yml");

    var tagPatterns = Sequence(Map(Map(workflow, "on"), "push"), "tags")
        .Children.Cast<YamlScalarNode>().Select(static value => value.Value!).ToArray();
    (tagPatterns).Should().Equal(["v*.*.*"]);
    AssertPermissions(
        workflow,
        ("attestations", "write"),
        ("contents", "read"),
        ("id-token", "write"),
        ("packages", "write"));
    (Scalar(Map(workflow, "concurrency"), "group")).Should().Be("release-ghcr-${{ github.repository }}");
    (Scalar(Map(workflow, "concurrency"), "cancel-in-progress")).Should().Be("false");

    var publishJob = Map(Map(workflow, "jobs"), "publish");
    (Scalar(publishJob, "environment")).Should().Be("release");

    var steps = Steps(workflow).ToArray();
    var validate = Step(steps, "Validate release identity and security gate");
    (Scalar(validate, "id")).Should().Be("release");
    (Scalar(validate, "run")).Should().Be("./scripts/validate-release.sh");
    var validateEnvironment = Map(validate, "env");
    (Scalar(validateEnvironment, "HEVY_CANONICAL_REPOSITORY")).Should().Be("${{ vars.HEVY_CANONICAL_REPOSITORY }}");
    (Scalar(validateEnvironment, "HEVY_PRIVATE_ADVISORY_VERIFIED")).Should().Be("${{ vars.HEVY_PRIVATE_ADVISORY_VERIFIED }}");

    var reproducibility = Step(steps, "Verify reproducible multi-architecture image");
    (Scalar(reproducibility, "id")).Should().Be("reproducibility");
    var reproducibilityEnvironment = Map(reproducibility, "env");
    (Scalar(reproducibilityEnvironment, "REVISION")).Should().Be("${{ steps.release.outputs.revision }}");
    (Scalar(reproducibilityEnvironment, "SOURCE_URL")).Should().Be("${{ steps.release.outputs.source }}");
    (Scalar(reproducibilityEnvironment, "VERSION")).Should().Be("${{ steps.release.outputs.version }}");
    (Scalar(reproducibility, "run")).Should().Be("./scripts/verify-reproducible-image.sh");

    var build = Step(steps, "Build and stage multi-architecture digest");
    (Scalar(build, "id")).Should().Be("build");
    (Scalar(Map(build, "env"), "SOURCE_DATE_EPOCH")).Should().Be("${{ steps.reproducibility.outputs.source_date_epoch }}");
    var buildWith = Map(build, "with");
    (Scalar(buildWith, "platforms")).Should().Be("linux/amd64,linux/arm64");
    (Scalar(buildWith, "tags")).Should().Be("${{ steps.release.outputs.image }}");
    var outputs = Scalar(buildWith, "outputs");
    (outputs).Should().Contain("push-by-digest=true");
    (outputs).Should().Contain("name-canonical=true");
    (outputs).Should().Contain("push=true");
    (outputs).Should().Contain("rewrite-timestamp=true");
    (outputs).Should().Contain("compatibility-version=30");
    (outputs).Should().Contain("oci-mediatypes=true");
    (outputs).Should().NotContain("steps.release.outputs.version");
    (Scalar(buildWith, "sbom")).Should().Be("false");
    (Scalar(buildWith, "provenance")).Should().Be("false");
    var buildArguments = Scalar(buildWith, "build-args");
    (buildArguments).Should().Contain("VERSION=${{ steps.release.outputs.version }}");
    (buildArguments).Should().Contain("REVISION=${{ steps.release.outputs.revision }}");
    (buildArguments).Should().Contain("SOURCE_URL=${{ steps.release.outputs.source }}");

    var imageVerification = Step(steps, "Verify published digest platforms labels and assembly version");
    (Scalar(imageVerification, "id")).Should().Be("image");
    var imageVerificationEnvironment = Map(imageVerification, "env");
    (Scalar(imageVerificationEnvironment, "REPRO_INDEX_DIGEST")).Should().Be("${{ steps.reproducibility.outputs.index_digest }}");
    (Scalar(imageVerificationEnvironment, "REPRO_AMD64_DIGEST")).Should().Be("${{ steps.reproducibility.outputs.amd64_digest }}");
    (Scalar(imageVerificationEnvironment, "REPRO_ARM64_DIGEST")).Should().Be("${{ steps.reproducibility.outputs.arm64_digest }}");
    var imageVerificationRun = Scalar(imageVerification, "run");
    (imageVerificationRun).Should().Contain("./scripts/capture-bounded-output.sh \"$index_file\" docker buildx imagetools inspect --raw \"$IMAGE@$IMAGE_DIGEST\"");
    (imageVerificationRun).Should().NotContain("imagetools inspect --raw \"$IMAGE@$IMAGE_DIGEST\" > \"$index_file\"");
    (imageVerificationRun).Should().Contain("./scripts/verify-staged-index.sh");
    var amd64Attestation = Step(steps, "Attest amd64 container SBOM");
    var arm64Attestation = Step(steps, "Attest arm64 container SBOM");
    (Scalar(amd64Attestation, "uses")).Should().StartWith("actions/attest-sbom@");
    (Scalar(arm64Attestation, "uses")).Should().StartWith("actions/attest-sbom@");
    (Map(amd64Attestation, "with").Children.Keys).Should().NotContain(new YamlScalarNode("create-storage-record"));
    (Map(arm64Attestation, "with").Children.Keys).Should().NotContain(new YamlScalarNode("create-storage-record"));
    (Scalar(Map(amd64Attestation, "with"), "subject-digest")).Should().Be("${{ steps.image.outputs.amd64_digest }}");
    (Scalar(Map(arm64Attestation, "with"), "subject-digest")).Should().Be("${{ steps.image.outputs.arm64_digest }}");

    var installSyft = Step(steps, "Install pinned Syft");
    (Scalar(installSyft, "run")).Should().Be("./scripts/install-syft.sh");
    var extractSboms = Step(steps, "Generate platform SPDX SBOMs");
    var extractSbomsRun = Scalar(extractSboms, "run");
    (extractSbomsRun).Should().Contain("syft \"registry:$IMAGE@$AMD64_DIGEST\"");
    (extractSbomsRun).Should().Contain("syft \"registry:$IMAGE@$ARM64_DIGEST\"");
    (extractSbomsRun).Should().Contain("./scripts/validate-spdx.sh");

    (Scalar(Step(steps, "Attest staged container provenance"), "uses")).Should().StartWith("actions/attest-build-provenance@");

    var tagCheck = Step(steps, "Authenticate GHCR version-tag lookup");
    (Scalar(Map(tagCheck, "env"), "GHCR_TOKEN")).Should().Be("${{ secrets.GITHUB_TOKEN }}");
    (Scalar(tagCheck, "run")).Should().Be("./scripts/ghcr-manifest.sh \"$IMAGE\" \"$RELEASE_VERSION\" >/dev/null");

    var runs = steps
        .Where(static step => step.Children.ContainsKey(new YamlScalarNode("run")))
        .Select(static step => Scalar(step, "run"))
        .ToArray();
    (runs).Should().AllSatisfy(static run => (run).Should().NotContain("${{"));
    (runs).Should().Contain((static run => run.Contains("cosign sign --yes", StringComparison.Ordinal)));
    (runs).Should().Contain((static run =>
        run.Contains("cosign verify", StringComparison.Ordinal) &&
        run.Contains("--certificate-github-workflow-sha \"$REVISION\"", StringComparison.Ordinal)));
    (runs).Should().Contain((static run =>
        run.Contains("gh attestation verify", StringComparison.Ordinal) &&
        run.Contains("--bundle-from-oci", StringComparison.Ordinal) &&
        run.Contains("--predicate-type \"$SPDX_PREDICATE_TYPE\"", StringComparison.Ordinal)));
    (runs).Should().Contain((static run => run.Contains("docker buildx imagetools inspect", StringComparison.Ordinal)));
    (runs).Should().Contain((static run => run.Contains("coproc MCP_SERVER", StringComparison.Ordinal)));
    (Scalar(buildWith, "tags")).Should().NotContainEquivalentOf("latest");

    var loginIndex = Array.IndexOf(steps, Step(steps, "Log in to GHCR"));
    (Array.IndexOf(steps, Step(steps, "Run real release container smokes")) < loginIndex).Should().BeTrue();
    (Array.IndexOf(steps, Step(steps, "Verify reproducible multi-architecture image")) < loginIndex).Should().BeTrue();
    (steps.Any(static step =>
        Scalar(step, "name") is "Build local release-check image" or "Inspect local release-check image")).Should().BeFalse();

    var promotion = Step(steps, "Promote verified digest to version tag");
    var promotionIndex = Array.IndexOf(steps, promotion);
    (promotionIndex).Should().Be(steps.Length - 1);
    (promotionIndex > Array.IndexOf(steps, Step(steps, "Verify GitHub attestations"))).Should().BeTrue();
    (promotionIndex > Array.IndexOf(steps, Step(steps, "Keylessly sign and verify the staged digest"))).Should().BeTrue();
    var promotionRun = Scalar(promotion, "run");
    (promotionRun).Should().Be("exec ./scripts/promote-ghcr-tag.sh \"$IMAGE\" \"$RELEASE_VERSION\" \"$IMAGE_DIGEST\"");

    (steps.Any(static step =>
        step.Children.TryGetValue(new YamlScalarNode("uses"), out var uses) &&
        ((YamlScalarNode)uses).Value!.StartsWith("actions/attest-build-provenance@", StringComparison.Ordinal))).Should().BeTrue();
    (steps.Any(static step =>
        step.Children.TryGetValue(new YamlScalarNode("uses"), out var uses) &&
        ((YamlScalarNode)uses).Value!.StartsWith("sigstore/cosign-installer@", StringComparison.Ordinal))).Should().BeTrue();

    var digestConsumers = steps
        .Where(static step => step.Children.TryGetValue(new YamlScalarNode("env"), out _))
        .Select(static step => Map(step, "env"))
        .Where(static environment => environment.Children.ContainsKey(new YamlScalarNode("IMAGE_DIGEST")))
        .ToArray();
    (digestConsumers).Should().NotBeEmpty();
    (digestConsumers).Should().AllSatisfy(static environment =>
        (Scalar(environment, "IMAGE_DIGEST")).Should().Be("${{ steps.build.outputs.digest }}"));

    var readme = File.ReadAllText(Path.Combine(RepositoryRoot, "README.md"));
    (readme).Should().Contain("--certificate-github-workflow-sha COMMIT_SHA");
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
    (Scalar(qemuWith, "image")).Should().Be($"{binfmt.GetProperty("image").GetString()}@sha256:{binfmt.GetProperty("sha256").GetString()}");

    var buildx = tools.GetProperty("buildx");
    var buildkit = tools.GetProperty("buildkit");
    var buildxWith = Map(Step(steps, "Set up Docker Buildx"), "with");
    (buildxWith.Children.Keys).Should().NotContain(new YamlScalarNode("version"));
    (Scalar(buildxWith, "driver-opts")).Should().Be($"image={buildkit.GetProperty("image").GetString()}@sha256:{buildkit.GetProperty("sha256").GetString()}");

    var ciSteps = Steps(Workflow("ci.yml")).ToArray();
    (Scalar(Map(Step(ciSteps, "Set up QEMU"), "with"), "image")).Should().Be($"{binfmt.GetProperty("image").GetString()}@sha256:{binfmt.GetProperty("sha256").GetString()}");
    var ciBuildxWith = Map(Step(ciSteps, "Set up Docker Buildx"), "with");
    (ciBuildxWith.Children.Keys).Should().NotContain(new YamlScalarNode("version"));
    (Scalar(ciBuildxWith, "driver-opts")).Should().Be($"image={buildkit.GetProperty("image").GetString()}@sha256:{buildkit.GetProperty("sha256").GetString()}");

    foreach (var tool in new[] { binfmt, buildkit })
    {
      (tool.GetProperty("sha256").GetString()!).Should().MatchRegex("^[0-9a-f]{64}$");
      (tool.GetProperty("source").GetString()).Should().StartWith("https://github.com/");
    }

    (buildx.GetProperty("commit").GetString()!).Should().MatchRegex("^[0-9a-f]{40}$");
    (buildx.GetProperty("sha256").GetString()!).Should().MatchRegex("^[0-9a-f]{64}$");
    (buildx.GetProperty("archive").GetString()).Should().Be($"buildx-v{buildx.GetProperty("version").GetString()}.linux-amd64");
    (Scalar(Step(steps, "Install pinned Buildx"), "run")).Should().Be("./scripts/install-buildx.sh");
    (Scalar(Step(ciSteps, "Install pinned Buildx"), "run")).Should().Be("./scripts/install-buildx.sh");
    (Scalar(Step(steps, "Verify pinned Buildx"), "run")).Should().Be("./scripts/verify-buildx-version.sh");
    (Scalar(Step(ciSteps, "Verify pinned Buildx"), "run")).Should().Be("./scripts/verify-buildx-version.sh");
    (Array.IndexOf(steps, Step(steps, "Install pinned Buildx")) < Array.IndexOf(steps, Step(steps, "Set up Docker Buildx"))).Should().BeTrue();
    (Array.IndexOf(steps, Step(steps, "Set up Docker Buildx")) < Array.IndexOf(steps, Step(steps, "Verify pinned Buildx"))).Should().BeTrue();
    (Array.IndexOf(ciSteps, Step(ciSteps, "Install pinned Buildx")) < Array.IndexOf(ciSteps, Step(ciSteps, "Set up Docker Buildx"))).Should().BeTrue();
    (Array.IndexOf(ciSteps, Step(ciSteps, "Set up Docker Buildx")) < Array.IndexOf(ciSteps, Step(ciSteps, "Verify pinned Buildx"))).Should().BeTrue();

    var syft = tools.GetProperty("syft");
    (syft.GetProperty("sha256").GetString()!).Should().MatchRegex("^[0-9a-f]{64}$");
    (syft.GetProperty("commit").GetString()!).Should().MatchRegex("^[0-9a-f]{40}$");
    (syft.GetProperty("source").GetString()).Should().Be($"https://github.com/anchore/syft/releases/tag/v{syft.GetProperty("version").GetString()}");
  }

  [Fact]
  public void EveryExternalActionUsesAnAuditedFullCommitPin()
  {
    var lockPath = Path.Combine(RepositoryRoot, ".github", "actions-lock.json");
    (File.Exists(lockPath)).Should().BeTrue("The audited action lock document must exist.");
    using var lockDocument = JsonDocument.Parse(File.ReadAllText(lockPath));
    var actions = lockDocument.RootElement.GetProperty("actions");

    var usesValues = new[] { Workflow("ci.yml"), Workflow("release.yml") }
        .SelectMany(Steps)
        .Where(static step => step.Children.ContainsKey(new YamlScalarNode("uses")))
        .Select(static step => Scalar(step, "uses"))
        .Where(static value => !value.StartsWith("./", StringComparison.Ordinal))
        .ToArray();

    (usesValues).Should().NotBeEmpty();
    foreach (var uses in usesValues)
    {
      var match = Regex.Match(uses, "^(?<action>[^@]+)@(?<commit>[0-9a-f]{40})$", RegexOptions.CultureInvariant);
      (match.Success).Should().BeTrue($"Action reference is not pinned by a full commit: {uses}");
      var action = match.Groups["action"].Value;
      var locked = actions.GetProperty(action);
      (locked.GetProperty("commit").GetString()).Should().Be(match.Groups["commit"].Value);
      var version = locked.GetProperty("version").GetString();
      (version!).Should().MatchRegex("^v[0-9]+(?:\\.[0-9]+){1,2}$");
      (locked.GetProperty("source").GetString()).Should().Be($"https://github.com/{action}/tree/{version}");
    }
  }

  [Fact]
  public void ActionlintLockRecordsAnAuditedReleaseChecksum()
  {
    var lockPath = Path.Combine(RepositoryRoot, ".github", "tools-lock.json");
    (File.Exists(lockPath)).Should().BeTrue("The audited CI-tool lock document must exist.");
    using var document = JsonDocument.Parse(File.ReadAllText(lockPath));
    var actionlint = document.RootElement.GetProperty("tools").GetProperty("actionlint");
    var version = actionlint.GetProperty("version").GetString()!;
    var checksum = actionlint.GetProperty("sha256").GetString()!;
    (checksum).Should().MatchRegex("^[0-9a-f]{64}$");
    (actionlint.GetProperty("source").GetString()).Should().Be($"https://github.com/rhysd/actionlint/releases/tag/v{version}");
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

    (result.ExitCode).Should().Be(0);
    var entries = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    (entries.Length).Should().Be(scripts.Length);
    (entries).Should().AllSatisfy(static entry => (entry).Should().StartWith("100755 "));
  }

  [Fact]
  public void RepositoryFormattingPolicyDocumentsDeliveryFileIndentation()
  {
    var editorConfig = File.ReadAllText(Path.Combine(RepositoryRoot, ".editorconfig"));

    (editorConfig).Should().Contain("[*.{json,sh,yaml,yml}]");
    (editorConfig[(editorConfig.IndexOf("[*.{json,sh,yaml,yml}]", StringComparison.Ordinal))..]).Should().MatchRegex("(?m)^indent_size = 2$");
  }

  [Fact]
  public void DependabotGroupsWeeklyStableUpdatesAndCannotSelectMcpTwo()
  {
    var dependabot = Yaml(Path.Combine(RepositoryRoot, ".github", "dependabot.yml"));
    (Scalar(dependabot, "version")).Should().Be("2");
    var updates = Sequence(dependabot, "updates").Children.Cast<YamlMappingNode>().ToArray();
    (updates.Select(static update => Scalar(update, "package-ecosystem")).Order(StringComparer.Ordinal).ToArray()).Should().Equal(["docker", "github-actions", "nuget"]);

    foreach (var update in updates)
    {
      (Scalar(update, "directory")).Should().Be("/");
      (Scalar(Map(update, "schedule"), "interval")).Should().Be("weekly");
      var stable = Map(Map(update, "groups"), "stable-updates");
      (Sequence(stable, "update-types").Children.Cast<YamlScalarNode>().Select(static value => value.Value!).Order(StringComparer.Ordinal).ToArray()).Should().Equal(["minor", "patch"]);
    }

    var nuget = updates.Single(static update => Scalar(update, "package-ecosystem") == "nuget");
    var ignored = Sequence(nuget, "ignore").Children.Cast<YamlMappingNode>().ToArray();
    (ignored.Select(static item => Scalar(item, "dependency-name")).Order(StringComparer.Ordinal).ToArray()).Should().Equal(["ModelContextProtocol", "ModelContextProtocol.AspNetCore"]);
    (ignored).Should().AllSatisfy(static item =>
    {
      (Sequence(item, "versions").Children.Cast<YamlScalarNode>().Select(static value => value.Value!).ToArray()).Should().Equal(["2.*"]);
      (Sequence(item, "update-types").Children.Cast<YamlScalarNode>().Select(static value => value.Value)).Should().Contain("version-update:semver-major");
    });

    var packages = File.ReadAllText(Path.Combine(RepositoryRoot, "Directory.Packages.props"));
    (packages).Should().Contain("<PackageVersion Include=\"ModelContextProtocol\" Version=\"1.4.1\" />");
    (packages).Should().Contain("<PackageVersion Include=\"ModelContextProtocol.AspNetCore\" Version=\"1.4.1\" />");
    (packages).Should().NotContainEquivalentOf("preview");
  }

  [Fact]
  public async Task ReleaseValidatorAcceptsLightweightAndAnnotatedExactTagsAndRejectsUnsafeIdentity()
  {
    var script = Path.Combine(RepositoryRoot, "scripts", "validate-release.sh");
    (File.Exists(script)).Should().BeTrue("The executable release validator must exist.");

    await using var repository = await TemporaryGitRepository.CreateAsync();
    foreach (var annotated in new[] { false, true })
    {
      var tag = annotated ? "v2.3.4" : "v1.2.3";
      await repository.TagAsync(tag, annotated);
      var result = await repository.RunValidatorAsync(script, tag, securityVerified: "true");
      (result.ExitCode).Should().Be(0);
      (result.OutputFile).Should().Contain($"version={tag[1..]}");
      (result.OutputFile).Should().Contain("image=ghcr.io/example/hevy-client");
      (result.OutputFile).Should().Contain($"revision={repository.Commit}");
      (result.OutputFile).Should().Contain("source=https://github.com/Example/Hevy-Client");
    }

    foreach (var invalidTag in new[] { "v1.2", "v1.2.3-rc.1", "v01.2.3", "1.2.3" })
    {
      var result = await repository.RunValidatorAsync(script, invalidTag, securityVerified: "true", createTag: false);
      (result.ExitCode).Should().NotBe(0);
    }

    var blocked = await repository.RunValidatorAsync(script, "v1.2.3", securityVerified: "false");
    (blocked.ExitCode).Should().NotBe(0);
    (blocked.StandardError).Should().ContainEquivalentOf("private vulnerability reporting");
  }

  [Fact]
  public async Task RepositoryAuditAcceptsThisTreeAndRejectsSecretsTelemetryForeignOriginsPlaceholdersAndArtifacts()
  {
    var script = Path.Combine(RepositoryRoot, "scripts", "audit-repository.sh");
    (File.Exists(script)).Should().BeTrue("The repeatable release audit must exist.");

    var current = await RunProcessAsync(RepositoryRoot, "/bin/sh", script, RepositoryRoot);
    (current.ExitCode).Should().Be(0);

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
      (unsafeResult.ExitCode).Should().NotBe(0);
      (unsafeResult.StandardError).Should().ContainEquivalentOf("secret");
      (unsafeResult.StandardError).Should().ContainEquivalentOf("credential assignment");
      (unsafeResult.StandardError).Should().ContainEquivalentOf("telemetry");
      (unsafeResult.StandardError).Should().ContainEquivalentOf("origin");
      (unsafeResult.StandardError).Should().ContainEquivalentOf("placeholder");
      (unsafeResult.StandardError).Should().ContainEquivalentOf("artifact");
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
  [InlineData("src/Inline.cs", "var value = 1; // trailing comment\n", "single-line comment")]
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

      (result.ExitCode).Should().NotBe(0);
      (result.StandardError).Should().ContainEquivalentOf(expectedFinding);
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
    (File.Exists(path)).Should().BeTrue($"Required parsed YAML file does not exist: {path}");
    var stream = new YamlStream();
    using var reader = File.OpenText(path);
    stream.Load(reader);
    (stream.Documents).Should().ContainSingle();
    return (stream.Documents[0].RootNode).Should().BeOfType<YamlMappingNode>().Which;
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
    (actualValues).Should().Equal(expectedValues);
  }

  private static IEnumerable<string> Keys(YamlMappingNode mapping) =>
      mapping.Children.Keys.Cast<YamlScalarNode>().Select(static key => key.Value!);

  private static YamlMappingNode Map(YamlMappingNode parent, string key) =>
      (parent.Children[new YamlScalarNode(key)]).Should().BeOfType<YamlMappingNode>().Which;

  private static YamlSequenceNode Sequence(YamlMappingNode parent, string key) =>
      (parent.Children[new YamlScalarNode(key)]).Should().BeOfType<YamlSequenceNode>().Which;

  private static string Scalar(YamlMappingNode parent, string key) =>
      (parent.Children[new YamlScalarNode(key)]).Should().BeOfType<YamlScalarNode>().Which.Value!;

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
    (result.ExitCode).Should().Be(0);
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
      (result.ExitCode).Should().Be(0);
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
