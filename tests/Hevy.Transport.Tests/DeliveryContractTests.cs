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

    var steps = Steps(workflow).ToArray();
    var validate = Step(steps, "Validate release identity and security gate");
    Assert.Equal("release", Scalar(validate, "id"));
    Assert.Equal("./scripts/validate-release.sh", Scalar(validate, "run"));
    var validateEnvironment = Map(validate, "env");
    Assert.Equal("${{ vars.HEVY_CANONICAL_REPOSITORY }}", Scalar(validateEnvironment, "HEVY_CANONICAL_REPOSITORY"));
    Assert.Equal("${{ vars.HEVY_PRIVATE_ADVISORY_VERIFIED }}", Scalar(validateEnvironment, "HEVY_PRIVATE_ADVISORY_VERIFIED"));

    var build = Step(steps, "Build and stage immutable multi-architecture digest");
    Assert.Equal("build", Scalar(build, "id"));
    var buildWith = Map(build, "with");
    Assert.Equal("linux/amd64,linux/arm64", Scalar(buildWith, "platforms"));
    Assert.Equal("${{ steps.release.outputs.image }}", Scalar(buildWith, "tags"));
    var outputs = Scalar(buildWith, "outputs");
    Assert.Contains("push-by-digest=true", outputs, StringComparison.Ordinal);
    Assert.Contains("name-canonical=true", outputs, StringComparison.Ordinal);
    Assert.Contains("push=true", outputs, StringComparison.Ordinal);
    Assert.DoesNotContain("steps.release.outputs.version", outputs, StringComparison.Ordinal);
    Assert.Equal("true", Scalar(buildWith, "sbom"));
    Assert.Equal("mode=max", Scalar(buildWith, "provenance"));
    var buildArguments = Scalar(buildWith, "build-args");
    Assert.Contains("VERSION=${{ steps.release.outputs.version }}", buildArguments, StringComparison.Ordinal);
    Assert.Contains("REVISION=${{ steps.release.outputs.revision }}", buildArguments, StringComparison.Ordinal);
    Assert.Contains("SOURCE_URL=${{ steps.release.outputs.source }}", buildArguments, StringComparison.Ordinal);

    var imageVerification = Step(steps, "Verify published digest platforms labels and assembly version");
    Assert.Equal("image", Scalar(imageVerification, "id"));
    var amd64Attestation = Step(steps, "Attest amd64 container SBOM");
    var arm64Attestation = Step(steps, "Attest arm64 container SBOM");
    Assert.Equal("${{ steps.image.outputs.amd64_digest }}", Scalar(Map(amd64Attestation, "with"), "subject-digest"));
    Assert.Equal("${{ steps.image.outputs.arm64_digest }}", Scalar(Map(arm64Attestation, "with"), "subject-digest"));

    var immutableTagCheck = Step(steps, "Refuse an existing immutable version tag");
    Assert.Equal("${{ secrets.GITHUB_TOKEN }}", Scalar(Map(immutableTagCheck, "env"), "GHCR_TOKEN"));
    var immutableTagRun = Scalar(immutableTagCheck, "run");
    Assert.Contains("--request HEAD", immutableTagRun, StringComparison.Ordinal);
    Assert.Contains("200)", immutableTagRun, StringComparison.Ordinal);
    Assert.Contains("404)", immutableTagRun, StringComparison.Ordinal);

    var runs = steps
        .Where(static step => step.Children.ContainsKey(new YamlScalarNode("run")))
        .Select(static step => Scalar(step, "run"))
        .ToArray();
    Assert.All(runs, static run => Assert.DoesNotContain("${{", run, StringComparison.Ordinal));
    Assert.Contains(runs, static run => run.Contains("^sha256:[0-9a-f]{64}$", StringComparison.Ordinal));
    Assert.Contains(runs, static run => run.Contains("cosign sign --yes", StringComparison.Ordinal));
    Assert.Contains(runs, static run => run.Contains("cosign verify", StringComparison.Ordinal));
    Assert.Contains(runs, static run =>
        run.Contains("gh attestation verify", StringComparison.Ordinal) &&
        run.Contains("--bundle-from-oci", StringComparison.Ordinal) &&
        run.Contains("https://spdx.dev/Document", StringComparison.Ordinal));
    Assert.Contains(runs, static run => run.Contains("docker buildx imagetools inspect", StringComparison.Ordinal));
    Assert.Contains(runs, static run => run.Contains("coproc MCP_SERVER", StringComparison.Ordinal));
    Assert.DoesNotContain(Scalar(buildWith, "tags"), "latest", StringComparison.OrdinalIgnoreCase);

    var promotion = Step(steps, "Promote verified digest to immutable version tag");
    var promotionIndex = Array.IndexOf(steps, promotion);
    Assert.Equal(steps.Length - 1, promotionIndex);
    Assert.True(promotionIndex > Array.IndexOf(steps, Step(steps, "Verify GitHub attestations")));
    Assert.True(promotionIndex > Array.IndexOf(steps, Step(steps, "Keylessly sign and verify the immutable digest")));
    var promotionRun = Scalar(promotion, "run");
    Assert.Contains("docker buildx imagetools create", promotionRun, StringComparison.Ordinal);
    Assert.Contains("$IMAGE@$IMAGE_DIGEST", promotionRun, StringComparison.Ordinal);
    Assert.Contains("$IMAGE:$RELEASE_VERSION", promotionRun, StringComparison.Ordinal);

    Assert.Contains(steps, static step =>
        step.Children.TryGetValue(new YamlScalarNode("uses"), out var uses) &&
        ((YamlScalarNode)uses).Value!.StartsWith("actions/attest@", StringComparison.Ordinal));
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
  public void EphemeralActionlintBinaryMatchesItsAuditedReleaseChecksum()
  {
    var lockPath = Path.Combine(RepositoryRoot, ".github", "tools-lock.json");
    Assert.True(File.Exists(lockPath), "The audited CI-tool lock document must exist.");
    using var document = JsonDocument.Parse(File.ReadAllText(lockPath));
    var actionlint = document.RootElement.GetProperty("tools").GetProperty("actionlint");
    var version = actionlint.GetProperty("version").GetString()!;
    var archive = actionlint.GetProperty("archive").GetString()!;
    var checksum = actionlint.GetProperty("sha256").GetString()!;
    Assert.Matches("^[0-9a-f]{64}$", checksum);
    Assert.Equal($"https://github.com/rhysd/actionlint/releases/tag/v{version}", actionlint.GetProperty("source").GetString());

    var installer = File.ReadAllText(Path.Combine(RepositoryRoot, "scripts", "run-actionlint.sh"));
    Assert.Contains($"version={version}", installer, StringComparison.Ordinal);
    Assert.Contains($"archive={archive}", installer, StringComparison.Ordinal);
    Assert.Contains($"checksum={checksum}", installer, StringComparison.Ordinal);
    Assert.Contains("sha256sum --check --status", installer, StringComparison.Ordinal);
    Assert.Contains("mktemp -d", installer, StringComparison.Ordinal);
  }

  [Fact]
  public async Task WorkflowSupportScriptsAreCommittedAsExecutables()
  {
    var scripts = new[]
    {
      "scripts/audit-repository.sh",
      "scripts/run-actionlint.sh",
      "scripts/validate-openapi.sh",
      "scripts/validate-release.sh",
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
        Assert.Contains("version-update:semver-major", Sequence(item, "update-types").Children.Cast<YamlScalarNode>().Select(static value => value.Value)));

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

  private static async Task<ProcessResult> RunProcessAsync(
      string workingDirectory,
      string executable,
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

  private sealed record ProcessResult(string StandardOutput, string StandardError, int ExitCode = 0);

  private sealed record ValidatorResult(
      int ExitCode,
      string StandardOutput,
      string StandardError,
      string OutputFile);
}
