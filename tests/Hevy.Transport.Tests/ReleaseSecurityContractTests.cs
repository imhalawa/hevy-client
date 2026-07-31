using System.Collections.Concurrent;
using System.Formats.Tar;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Hevy.Transport.Tests;

public sealed class ReleaseSecurityContractTests
{
  private const int OciIndexByteLimit = 4_194_304;
  private const string ExistingDigest = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
  private const string IntendedDigest = "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
  private static readonly string RepositoryRoot = DockerProcess.RepositoryRoot;

  [Theory]
  [InlineData(404, "absent")]
  [InlineData(200, $"present {ExistingDigest}")]
  public async Task GhcrProbeCompletesTheBearerChallengeWithoutLeakingCredentials(
      int authenticatedStatus,
      string expectedOutput)
  {
    var script = GhcrProbeScript();
    var secret = string.Concat("ghs.fixture.segment_", "7Qm4N2x9Vp6K8s3R5t1W");
    await using var registry = RegistryFixture.Start(new RegistryScenario(AuthenticatedStatus: authenticatedStatus));
    await using var fixture = await CurlFixture.CreateAsync();

    var result = await RunScriptAsync(
        script,
        ["ghcr.io/example/hevy-client", "1.2.3"],
        fixture.Environment(registry.BaseUri, secret));

    (result.ExitCode).Should().Be(0);
    (result.StandardOutput.Trim()).Should().Be(expectedOutput);
    (result.StandardOutput).Should().NotContain(secret);
    (result.StandardError).Should().NotContain(secret);
    (await File.ReadAllTextAsync(fixture.ArgumentLog)).Should().NotContain(secret);

    var requests = registry.Requests.ToArray();
    (requests.Length).Should().Be(3);
    (requests[0].Headers.ContainsKey("Authorization")).Should().BeFalse();
    (HasBasicCredentials(requests[1], "release-actor", secret)).Should().BeTrue("The scoped token request did not carry the expected credentials.");
    (requests[2].Headers["Authorization"]).Should().Be("Bearer fixture-registry-bearer-token");
  }

  [Theory]
  [InlineData(401)]
  [InlineData(403)]
  [InlineData(500)]
  [InlineData(418)]
  public async Task GhcrProbeFailsClosedForEveryUnexpectedAuthenticatedStatus(int authenticatedStatus)
  {
    var script = GhcrProbeScript();
    await using var registry = RegistryFixture.Start(new RegistryScenario(AuthenticatedStatus: authenticatedStatus));
    await using var fixture = await CurlFixture.CreateAsync();

    var result = await RunScriptAsync(
        script,
        ["ghcr.io/example/hevy-client", "1.2.3"],
        fixture.Environment(registry.BaseUri, "fixture-registry-secret"));

    (result.ExitCode).Should().NotBe(0);
    (result.StandardError).Should().ContainEquivalentOf("authenticated manifest probe failed");
  }

  [Theory]
  [InlineData(true, 200, false)]
  [InlineData(false, 500, false)]
  [InlineData(false, 200, true)]
  public async Task GhcrProbeFailsClosedForMalformedChallengeOrTokenExchange(
      bool malformedChallenge,
      int tokenStatus,
      bool malformedToken)
  {
    var script = GhcrProbeScript();
    await using var registry = RegistryFixture.Start(
        new RegistryScenario(malformedChallenge, tokenStatus, malformedToken));
    await using var fixture = await CurlFixture.CreateAsync();

    var result = await RunScriptAsync(
        script,
        ["ghcr.io/example/hevy-client", "1.2.3"],
        fixture.Environment(registry.BaseUri, "fixture-registry-secret"));

    (result.ExitCode).Should().NotBe(0);
    (result.StandardError).Should().NotContain("fixture-registry-secret");
  }

  [Fact]
  public async Task GhcrProbeAcceptsReorderedChallengeParametersAndIgnoresSafeExtensions()
  {
    var script = GhcrProbeScript();
    await using var registry = RegistryFixture.Start(new RegistryScenario(ReorderedChallenge: true));
    await using var fixture = await CurlFixture.CreateAsync();

    var result = await RunScriptAsync(
        script,
        ["ghcr.io/example/hevy-client", "1.2.3"],
        fixture.Environment(registry.BaseUri, "fixture-registry-secret"));

    (result.ExitCode).Should().Be(0);
    (result.StandardOutput.Trim()).Should().Be("absent");
  }

  [Fact]
  public async Task GhcrProbeRejectsDuplicateRequiredChallengeParameters()
  {
    var script = GhcrProbeScript();
    await using var registry = RegistryFixture.Start(new RegistryScenario(DuplicateScope: true));
    await using var fixture = await CurlFixture.CreateAsync();

    var result = await RunScriptAsync(
        script,
        ["ghcr.io/example/hevy-client", "1.2.3"],
        fixture.Environment(registry.BaseUri, "fixture-registry-secret"));

    (result.ExitCode).Should().NotBe(0);
    (result.StandardError).Should().ContainEquivalentOf("duplicate");
  }

  [Fact]
  public async Task GhcrProbeFailsClosedWhenTheRegistryCannotBeReached()
  {
    var script = GhcrProbeScript();
    await using var fixture = await CurlFixture.CreateAsync();
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();

    var result = await RunScriptAsync(
        script,
        ["ghcr.io/example/hevy-client", "1.2.3"],
        fixture.Environment(new Uri($"http://127.0.0.1:{port}"), "fixture-registry-secret"));

    (result.ExitCode).Should().NotBe(0);
    (result.StandardError).Should().ContainEquivalentOf("registry challenge probe failed");
  }

  [Fact]
  public async Task SpdxValidatorRequiresVersion23AndEmitsItsExactPredicateType()
  {
    var script = Path.Combine(RepositoryRoot, "scripts", "validate-spdx.sh");
    (File.Exists(script)).Should().BeTrue("The executable SPDX validator must exist.");
    var fixture = Path.Combine(Path.GetTempPath(), $"hevy-spdx-{Guid.NewGuid():N}");
    Directory.CreateDirectory(fixture);
    try
    {
      var amd64 = Path.Combine(fixture, "amd64.spdx.json");
      var arm64 = Path.Combine(fixture, "arm64.spdx.json");
      var output = Path.Combine(fixture, "github-output.txt");
      const string validSpdx = """
          {
            "SPDXID": "SPDXRef-DOCUMENT",
            "spdxVersion": "SPDX-2.3",
            "dataLicense": "CC0-1.0",
            "name": "hevy-client-linux-amd64",
            "documentNamespace": "https://example.invalid/spdx/hevy-client/fixture",
            "creationInfo": {
              "created": "2026-07-26T09:00:00Z",
              "creators": ["Tool: fixture-generator-1.0"]
            },
            "packages": [
              {
                "SPDXID": "SPDXRef-Package-hevy-client",
                "name": "hevy-client",
                "downloadLocation": "NOASSERTION",
                "filesAnalyzed": false
              }
            ],
            "relationships": [
              {
                "spdxElementId": "SPDXRef-DOCUMENT",
                "relationshipType": "DESCRIBES",
                "relatedSpdxElement": "SPDXRef-Package-hevy-client"
              }
            ]
          }
          """;
      await File.WriteAllTextAsync(amd64, validSpdx);
      await File.WriteAllTextAsync(arm64, validSpdx);

      var accepted = await RunScriptAsync(
          script,
          [amd64, arm64],
          new Dictionary<string, string?> { ["GITHUB_OUTPUT"] = output });
      (accepted.ExitCode).Should().Be(0);
      (await File.ReadAllTextAsync(output)).Should().Be("predicate_type=https://spdx.dev/Document/v2.3\n");

      foreach (var invalidSpdx in new[]
      {
        "{\"spdxVersion\":\"SPDX-2.3\"}\n",
        validSpdx.Replace("SPDX-2.3", "SPDX-2.2", StringComparison.Ordinal),
        validSpdx.Replace(
            "\"relatedSpdxElement\": \"SPDXRef-Package-hevy-client\"",
            "\"relatedSpdxElement\": \"SPDXRef-Package-missing\"",
            StringComparison.Ordinal),
      })
      {
        await File.WriteAllTextAsync(arm64, invalidSpdx);
        var rejected = await RunScriptAsync(script, [amd64, arm64], environment: null);
        (rejected.ExitCode).Should().NotBe(0);
        (rejected.StandardError).Should().Contain("complete SPDX-2.3");
      }
    }
    finally
    {
      Directory.Delete(fixture, recursive: true);
    }
  }

  [Fact]
  public async Task Sha256DigestValidatorChecksEveryProvidedDigest()
  {
    var script = Path.Combine(RepositoryRoot, "scripts", "validate-sha256-digest.sh");
    (File.Exists(script)).Should().BeTrue("The executable SHA-256 digest validator must exist.");
    var accepted = await RunScriptAsync(script, [ExistingDigest, IntendedDigest], environment: null);
    (accepted.ExitCode).Should().Be(0);

    var rejected = await RunScriptAsync(script, [ExistingDigest, "sha256:not-a-digest"], environment: null);
    (rejected.ExitCode).Should().NotBe(0);
    (rejected.StandardError).Should().Contain("SHA-256 digest");

    var rejectedMultiline = await RunScriptAsync(
        script,
        [ExistingDigest + "\ntrailing-value"],
        environment: null);
    (rejectedMultiline.ExitCode).Should().NotBe(0);
    (rejectedMultiline.StandardError).Should().Contain("SHA-256 digest");
  }

  [Fact]
  public async Task ReproducibilityGateExecutesExactlyTwoHardenedExportsAndPublishesVerifiedDigests()
  {
    var fixture = await ReproducibilityFixture.CreateAsync(mismatch: false, extraDescriptor: false);
    try
    {
      var result = await fixture.RunAsync();

      (result.ExitCode).Should().Be(0);
      (await File.ReadAllTextAsync(fixture.BuildLog)).Should().Be(fixture.ExpectedBuildLog);
      (await File.ReadAllTextAsync(fixture.GitHubOutput)).Should().Be($"sentinel=preserve\nsource_date_epoch=1770000000\nindex_digest={fixture.IndexDigest}\namd64_digest={ExistingDigest}\narm64_digest={IntendedDigest}\n");
      (Directory.GetFileSystemEntries(fixture.TemporaryRoot)).Should().BeEmpty();
    }
    finally
    {
      fixture.Dispose();
    }
  }

  [Theory]
  [InlineData(true, false, "not reproducible")]
  [InlineData(false, true, "exactly two")]
  public async Task ReproducibilityGateRejectsMismatchAndExtraDescriptorsWithoutPublishingOutputs(
      bool mismatch,
      bool extraDescriptor,
      string expectedError)
  {
    var fixture = await ReproducibilityFixture.CreateAsync(mismatch, extraDescriptor);
    try
    {
      var result = await fixture.RunAsync();

      (result.ExitCode).Should().NotBe(0);
      (result.StandardError).Should().ContainEquivalentOf(expectedError);
      (await File.ReadAllTextAsync(fixture.GitHubOutput)).Should().Be("sentinel=preserve\n");
      (Directory.GetFileSystemEntries(fixture.TemporaryRoot)).Should().BeEmpty();
    }
    finally
    {
      fixture.Dispose();
    }
  }

  [Fact]
  public async Task ReproducibilityGateCleansUpWhenBuildxFails()
  {
    var fixture = await ReproducibilityFixture.CreateAsync(mismatch: false, extraDescriptor: false);
    try
    {
      var result = await fixture.RunAsync(failOnBuild: "2");

      (result.ExitCode).Should().NotBe(0);
      (await File.ReadAllTextAsync(fixture.GitHubOutput)).Should().Be("sentinel=preserve\n");
      (Directory.GetFileSystemEntries(fixture.TemporaryRoot)).Should().BeEmpty();
    }
    finally
    {
      fixture.Dispose();
    }
  }

  [Theory]
  [InlineData(OciIndexByteLimit - 1)]
  [InlineData(OciIndexByteLimit)]
  public async Task BoundedCaptureAtomicallyAcceptsOutputThroughTheExactLimit(int byteCount)
  {
    var fixture = Path.Combine(Path.GetTempPath(), $"hevy-bounded-capture-{Guid.NewGuid():N}");
    Directory.CreateDirectory(fixture);
    try
    {
      var source = Path.Combine(fixture, "source.bin");
      await using (var stream = new FileStream(source, FileMode.CreateNew, FileAccess.Write))
      {
        stream.SetLength(byteCount);
      }
      var output = Path.Combine(fixture, "index.json");
      var githubOutput = Path.Combine(fixture, "github-output.txt");
      await File.WriteAllTextAsync(output, "old-output");
      await File.WriteAllTextAsync(githubOutput, "sentinel=preserve\n");

      var result = await RunScriptAsync(
          Path.Combine(RepositoryRoot, "scripts", "capture-bounded-output.sh"),
          [output, "/bin/cat", source],
          new Dictionary<string, string?> { ["GITHUB_OUTPUT"] = githubOutput });

      (result.ExitCode).Should().Be(0);
      (new FileInfo(output).Length).Should().Be(byteCount);
      (await File.ReadAllTextAsync(githubOutput)).Should().Be("sentinel=preserve\n");
      (Directory.GetFiles(fixture, ".index.json.tmp.*")).Should().BeEmpty();
    }
    finally
    {
      Directory.Delete(fixture, recursive: true);
    }
  }

  [Fact]
  public async Task BoundedCaptureRejectsByteAfterLimitWithoutReplacingOutputOrPublishing()
  {
    var fixture = Path.Combine(Path.GetTempPath(), $"hevy-bounded-capture-{Guid.NewGuid():N}");
    Directory.CreateDirectory(fixture);
    try
    {
      var source = Path.Combine(fixture, "source.bin");
      await using (var stream = new FileStream(source, FileMode.CreateNew, FileAccess.Write))
      {
        stream.SetLength(OciIndexByteLimit + 1L);
      }
      var output = Path.Combine(fixture, "index.json");
      var githubOutput = Path.Combine(fixture, "github-output.txt");
      await File.WriteAllTextAsync(output, "old-output");
      await File.WriteAllTextAsync(githubOutput, "sentinel=preserve\n");

      var result = await RunScriptAsync(
          Path.Combine(RepositoryRoot, "scripts", "capture-bounded-output.sh"),
          [output, "/bin/cat", source],
          new Dictionary<string, string?> { ["GITHUB_OUTPUT"] = githubOutput });

      (result.ExitCode).Should().NotBe(0);
      (await File.ReadAllTextAsync(output)).Should().Be("old-output");
      (await File.ReadAllTextAsync(githubOutput)).Should().Be("sentinel=preserve\n");
      (Directory.GetFiles(fixture, ".index.json.tmp.*")).Should().BeEmpty();
    }
    finally
    {
      Directory.Delete(fixture, recursive: true);
    }
  }

  [Fact]
  public async Task BoundedCapturePreservesProducerFailureWithoutReplacingOutputOrPublishing()
  {
    var fixture = Path.Combine(Path.GetTempPath(), $"hevy-bounded-capture-{Guid.NewGuid():N}");
    Directory.CreateDirectory(fixture);
    try
    {
      var producer = Path.Combine(fixture, "producer");
      await File.WriteAllTextAsync(producer, "#!/bin/sh\nprintf 'partial-output'\nexit 23\n");
      MakeExecutable(producer);
      var output = Path.Combine(fixture, "index.json");
      var githubOutput = Path.Combine(fixture, "github-output.txt");
      await File.WriteAllTextAsync(output, "old-output");
      await File.WriteAllTextAsync(githubOutput, "sentinel=preserve\n");

      var result = await RunScriptAsync(
          Path.Combine(RepositoryRoot, "scripts", "capture-bounded-output.sh"),
          [output, producer],
          new Dictionary<string, string?> { ["GITHUB_OUTPUT"] = githubOutput });

      (result.ExitCode).Should().Be(23);
      (await File.ReadAllTextAsync(output)).Should().Be("old-output");
      (await File.ReadAllTextAsync(githubOutput)).Should().Be("sentinel=preserve\n");
      (Directory.GetFiles(fixture, ".index.json.tmp.*")).Should().BeEmpty();
    }
    finally
    {
      Directory.Delete(fixture, recursive: true);
    }
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task BoundedCaptureRejectsDirectoryDestinationsWithoutNestingTemporaryFiles(bool useDirectorySymlink)
  {
    var fixture = Path.Combine(Path.GetTempPath(), $"hevy-bounded-capture-{Guid.NewGuid():N}");
    Directory.CreateDirectory(fixture);
    try
    {
      var destinationDirectory = Path.Combine(fixture, "destination");
      Directory.CreateDirectory(destinationDirectory);
      var output = destinationDirectory;
      if (useDirectorySymlink)
      {
        output = Path.Combine(fixture, "index.json");
        Directory.CreateSymbolicLink(output, destinationDirectory);
      }
      var githubOutput = Path.Combine(fixture, "github-output.txt");
      await File.WriteAllTextAsync(githubOutput, "sentinel=preserve\n");

      var result = await RunScriptAsync(
          Path.Combine(RepositoryRoot, "scripts", "capture-bounded-output.sh"),
          [output, "/bin/printf", "new-output"],
          new Dictionary<string, string?> { ["GITHUB_OUTPUT"] = githubOutput });

      (result.ExitCode).Should().NotBe(0);
      (Directory.Exists(output)).Should().BeTrue();
      (Directory.GetFileSystemEntries(destinationDirectory)).Should().BeEmpty();
      (await File.ReadAllTextAsync(githubOutput)).Should().Be("sentinel=preserve\n");
      (Directory.GetFiles(fixture, ".index.json.tmp.*")).Should().BeEmpty();
      (Directory.GetFiles(fixture, ".destination.tmp.*")).Should().BeEmpty();
    }
    finally
    {
      Directory.Delete(fixture, recursive: true);
    }
  }

  [Fact]
  public async Task BoundedCaptureReportsReaderFailureInsteadOfConsequentialProducerSigpipe()
  {
    var fixture = Path.Combine(Path.GetTempPath(), $"hevy-bounded-capture-{Guid.NewGuid():N}");
    Directory.CreateDirectory(fixture);
    try
    {
      var source = Path.Combine(fixture, "source.bin");
      await using (var stream = new FileStream(source, FileMode.CreateNew, FileAccess.Write))
      {
        stream.SetLength(OciIndexByteLimit);
      }
      var binaries = Path.Combine(fixture, "bin");
      Directory.CreateDirectory(binaries);
      var failingHead = Path.Combine(binaries, "head");
      await File.WriteAllTextAsync(failingHead, "#!/bin/sh\n/bin/head -c 1 >/dev/null\nexit 37\n");
      MakeExecutable(failingHead);
      var producer = Path.Combine(fixture, "producer");
      await File.WriteAllTextAsync(producer, "#!/bin/sh\ntrap - PIPE\nexec /bin/cat \"$1\"\n");
      MakeExecutable(producer);
      var output = Path.Combine(fixture, "index.json");
      var githubOutput = Path.Combine(fixture, "github-output.txt");
      await File.WriteAllTextAsync(output, "old-output");
      await File.WriteAllTextAsync(githubOutput, "sentinel=preserve\n");

      var result = await RunScriptAsync(
          Path.Combine(RepositoryRoot, "scripts", "capture-bounded-output.sh"),
          [output, producer, source],
          new Dictionary<string, string?>
          {
            ["GITHUB_OUTPUT"] = githubOutput,
            ["PATH"] = $"{binaries}{Path.PathSeparator}{System.Environment.GetEnvironmentVariable("PATH")}",
          });

      (result.ExitCode).Should().Be(37);
      (await File.ReadAllTextAsync(output)).Should().Be("old-output");
      (await File.ReadAllTextAsync(githubOutput)).Should().Be("sentinel=preserve\n");
      (Directory.GetFiles(fixture, ".index.json.tmp.*")).Should().BeEmpty();
    }
    finally
    {
      Directory.Delete(fixture, recursive: true);
    }
  }

  [Theory]
  [InlineData("valid")]
  [InlineData("extra")]
  [InlineData("unknown")]
  [InlineData("concatenated_roots")]
  [InlineData("trailing_json")]
  [InlineData("trailing_garbage")]
  [InlineData("newline_digest")]
  [InlineData("duplicate_amd64")]
  [InlineData("fractional_size")]
  [InlineData("large_fractional_size")]
  [InlineData("string_schema")]
  [InlineData("zero_size")]
  [InlineData("negative_size")]
  [InlineData("max_size")]
  [InlineData("too_large_size")]
  public async Task OciIndexValidatorRequiresOneCompleteStrictTwoPlatformDocument(string scenario)
  {
    var fixture = Path.Combine(Path.GetTempPath(), $"hevy-index-{Guid.NewGuid():N}");
    Directory.CreateDirectory(fixture);
    try
    {
      var index = Path.Combine(fixture, "index.json");
      var bytes = CreateAdversarialPlatformIndex(scenario);
      await File.WriteAllBytesAsync(index, bytes);
      var digest = Sha256Digest(bytes);
      var script = Path.Combine(RepositoryRoot, "scripts", "validate-oci-index.sh");

      var result = await RunScriptAsync(script, [index, digest], environment: null);

      if (scenario is "valid" or "max_size")
      {
        (result.ExitCode).Should().Be(0);
        (result.StandardOutput).Should().Be($"amd64_digest={ExistingDigest}\narm64_digest={IntendedDigest}\n");
      }
      else
      {
        (result.ExitCode).Should().NotBe(0);
        (result.StandardError).Should().ContainEquivalentOf("OCI index");
        (result.StandardOutput).Should().Be(string.Empty);
      }
    }
    finally
    {
      Directory.Delete(fixture, recursive: true);
    }
  }

  [Theory]
  [InlineData(OciIndexByteLimit - 1)]
  [InlineData(OciIndexByteLimit)]
  public async Task OciIndexValidatorAcceptsValidDocumentsThroughTheExactByteLimit(int byteCount)
  {
    var fixture = Path.Combine(Path.GetTempPath(), $"hevy-index-size-{Guid.NewGuid():N}");
    Directory.CreateDirectory(fixture);
    try
    {
      var index = Path.Combine(fixture, "index.json");
      var bytes = PadJsonDocument(CreatePlatformIndex(extraDescriptor: false, unknownDescriptor: false), byteCount);
      await File.WriteAllBytesAsync(index, bytes);

      var result = await RunScriptAsync(
          Path.Combine(RepositoryRoot, "scripts", "validate-oci-index.sh"),
          [index, Sha256Digest(bytes)],
          environment: null);

      (result.ExitCode).Should().Be(0);
      (result.StandardOutput).Should().Be($"amd64_digest={ExistingDigest}\narm64_digest={IntendedDigest}\n");
    }
    finally
    {
      Directory.Delete(fixture, recursive: true);
    }
  }

  [Fact]
  public async Task OciIndexValidatorRejectsByteAfterTheExactLimitBeforeParsing()
  {
    var fixture = Path.Combine(Path.GetTempPath(), $"hevy-index-size-{Guid.NewGuid():N}");
    Directory.CreateDirectory(fixture);
    try
    {
      var index = Path.Combine(fixture, "index.json");
      var bytes = PadJsonDocument(
          CreatePlatformIndex(extraDescriptor: false, unknownDescriptor: false),
          OciIndexByteLimit + 1);
      await File.WriteAllBytesAsync(index, bytes);

      var result = await RunScriptAsync(
          Path.Combine(RepositoryRoot, "scripts", "validate-oci-index.sh"),
          [index, Sha256Digest(bytes)],
          environment: null);

      (result.ExitCode).Should().NotBe(0);
      (result.StandardError).Should().ContainEquivalentOf("OCI index");
      (result.StandardOutput).Should().Be(string.Empty);
    }
    finally
    {
      Directory.Delete(fixture, recursive: true);
    }
  }

  [Fact]
  public async Task ReproducibilityGateRejectsMultipleJsonRootsWithoutTouchingExistingOutputs()
  {
    var fixture = await ReproducibilityFixture.CreateAsync(
        mismatch: false,
        extraDescriptor: false,
        invalidIndexScenario: "concatenated_roots");
    try
    {
      var result = await fixture.RunAsync();

      (result.ExitCode).Should().NotBe(0);
      (result.StandardError).Should().ContainEquivalentOf("OCI index");
      (await File.ReadAllTextAsync(fixture.GitHubOutput)).Should().Be("sentinel=preserve\n");
      (Directory.GetFileSystemEntries(fixture.TemporaryRoot)).Should().BeEmpty();
    }
    finally
    {
      fixture.Dispose();
    }
  }

  [Fact]
  public async Task ReproducibilityGateRejectsLexicallyFractionalOuterSizeWithoutTouchingExistingOutputs()
  {
    var fixture = await ReproducibilityFixture.CreateAsync(
        mismatch: false,
        extraDescriptor: false,
        invalidOuterSize: true);
    try
    {
      var result = await fixture.RunAsync();

      (result.ExitCode).Should().NotBe(0);
      (result.StandardError).Should().ContainEquivalentOf("OCI archive index");
      (await File.ReadAllTextAsync(fixture.GitHubOutput)).Should().Be("sentinel=preserve\n");
      (Directory.GetFileSystemEntries(fixture.TemporaryRoot)).Should().BeEmpty();
    }
    finally
    {
      fixture.Dispose();
    }
  }

  [Fact]
  public async Task ReproducibilityGateRejectsStringOuterSchemaWithoutTouchingExistingOutputs()
  {
    var fixture = await ReproducibilityFixture.CreateAsync(
        mismatch: false,
        extraDescriptor: false,
        invalidOuterSchema: true);
    try
    {
      var result = await fixture.RunAsync();

      (result.ExitCode).Should().NotBe(0);
      (result.StandardError).Should().ContainEquivalentOf("OCI archive index");
      (await File.ReadAllTextAsync(fixture.GitHubOutput)).Should().Be("sentinel=preserve\n");
      (Directory.GetFileSystemEntries(fixture.TemporaryRoot)).Should().BeEmpty();
    }
    finally
    {
      fixture.Dispose();
    }
  }

  [Theory]
  [InlineData("9223372036854775807", true)]
  [InlineData("9223372036854775808", false)]
  public async Task ReproducibilityGateBoundsTheRawOuterDescriptorSizeToken(
      string outerSizeToken,
      bool shouldSucceed)
  {
    var fixture = await ReproducibilityFixture.CreateAsync(
        mismatch: false,
        extraDescriptor: false,
        outerSizeToken: outerSizeToken);
    try
    {
      var result = await fixture.RunAsync();

      (result.ExitCode == 0 ? 0 : 1).Should().Be(shouldSucceed ? 0 : 1);
      (await File.ReadAllTextAsync(fixture.GitHubOutput)).Should().Be(shouldSucceed
              ? $"sentinel=preserve\nsource_date_epoch=1770000000\nindex_digest={fixture.IndexDigest}\namd64_digest={ExistingDigest}\narm64_digest={IntendedDigest}\n"
              : "sentinel=preserve\n");
      (Directory.GetFileSystemEntries(fixture.TemporaryRoot)).Should().BeEmpty();
    }
    finally
    {
      fixture.Dispose();
    }
  }

  [Theory]
  [InlineData(OciIndexByteLimit, true)]
  [InlineData(OciIndexByteLimit + 1, false)]
  public async Task ReproducibilityGateBoundsTheOuterIndexDocumentBeforeParsing(
      int outerDocumentSize,
      bool shouldSucceed)
  {
    var fixture = await ReproducibilityFixture.CreateAsync(
        mismatch: false,
        extraDescriptor: false,
        outerDocumentSize: outerDocumentSize);
    try
    {
      var result = await fixture.RunAsync();

      (result.ExitCode == 0 ? 0 : 1).Should().Be(shouldSucceed ? 0 : 1);
      (await File.ReadAllTextAsync(fixture.GitHubOutput)).Should().Be(shouldSucceed
              ? $"sentinel=preserve\nsource_date_epoch=1770000000\nindex_digest={fixture.IndexDigest}\namd64_digest={ExistingDigest}\narm64_digest={IntendedDigest}\n"
              : "sentinel=preserve\n");
      (Directory.GetFileSystemEntries(fixture.TemporaryRoot)).Should().BeEmpty();
    }
    finally
    {
      fixture.Dispose();
    }
  }

  [Fact]
  public async Task ActionlintRunnerDownloadsChecksExecutesExactArgumentsAndCleansUp()
  {
    var pin = ReadActionlintPin();
    var fixture = await ActionlintFixture.CreateAsync(toolPresent: true);
    try
    {
      var result = await fixture.RunAsync(downloadSucceeds: true, checksumSucceeds: true, actionlintExitCode: 0);

      (result.ExitCode).Should().Be(0);
      (await File.ReadAllTextAsync(fixture.DownloadLog)).Should().Be($"--fail\n--location\n--proto\n=https\n--tlsv1.2\n--output\n{pin.Archive}\nhttps://github.com/rhysd/actionlint/releases/download/v{pin.Version}/{pin.Archive}\n");
      (await File.ReadAllTextAsync(fixture.ChecksumLog)).Should().Be($"arguments=--check --status\nchecksum={pin.Checksum}\nfile={pin.Archive}\n");
      (await File.ReadAllTextAsync(fixture.ExecutionLog)).Should().Be("-color\n");
      (Directory.GetFileSystemEntries(fixture.TemporaryRoot)).Should().BeEmpty();
    }
    finally
    {
      fixture.Dispose();
    }
  }

  [Theory]
  [InlineData("network")]
  [InlineData("checksum")]
  [InlineData("missing_tool")]
  public async Task ActionlintRunnerFailsClosedBeforeAnyUnverifiedExecution(string scenario)
  {
    var fixture = await ActionlintFixture.CreateAsync(toolPresent: scenario != "missing_tool");
    try
    {
      var result = await fixture.RunAsync(
          downloadSucceeds: scenario != "network",
          checksumSucceeds: scenario != "checksum",
          actionlintExitCode: 0);

      (result.ExitCode).Should().NotBe(0);
      (File.Exists(fixture.ExecutionLog)).Should().BeFalse();
      if (scenario == "network")
      {
        (File.Exists(fixture.ChecksumLog)).Should().BeFalse();
      }
      (Directory.GetFileSystemEntries(fixture.TemporaryRoot)).Should().BeEmpty();
    }
    finally
    {
      fixture.Dispose();
    }
  }

  [Fact]
  public async Task ActionlintRunnerReturnsTheVerifiedExecutableResult()
  {
    var fixture = await ActionlintFixture.CreateAsync(toolPresent: true);
    try
    {
      var result = await fixture.RunAsync(downloadSucceeds: true, checksumSucceeds: true, actionlintExitCode: 17);

      (result.ExitCode).Should().Be(17);
      (await File.ReadAllTextAsync(fixture.ExecutionLog)).Should().Be("-color\n");
      (Directory.GetFileSystemEntries(fixture.TemporaryRoot)).Should().BeEmpty();
    }
    finally
    {
      fixture.Dispose();
    }
  }

  [Theory]
  [InlineData("top")]
  [InlineData("amd64")]
  [InlineData("arm64")]
  public async Task StagedIndexVerifierRejectsEachGateDigestMismatch(string mismatch)
  {
    var fixture = Path.Combine(Path.GetTempPath(), $"hevy-staged-{Guid.NewGuid():N}");
    Directory.CreateDirectory(fixture);
    try
    {
      var index = Path.Combine(fixture, "index.json");
      var bytes = CreatePlatformIndex(extraDescriptor: false, unknownDescriptor: false);
      await File.WriteAllBytesAsync(index, bytes);
      var digest = Sha256Digest(bytes);
      var output = Path.Combine(fixture, "github-output.txt");
      await File.WriteAllTextAsync(output, string.Empty);
      var expectedTop = mismatch == "top" ? ExistingDigest : digest;
      var expectedAmd64 = mismatch == "amd64" ? IntendedDigest : ExistingDigest;
      var expectedArm64 = mismatch == "arm64" ? ExistingDigest : IntendedDigest;

      var result = await RunScriptAsync(
          Path.Combine(RepositoryRoot, "scripts", "verify-staged-index.sh"),
          [index, digest, expectedTop, expectedAmd64, expectedArm64],
          new Dictionary<string, string?> { ["GITHUB_OUTPUT"] = output });

      (result.ExitCode).Should().NotBe(0);
      (result.StandardError).Should().ContainEquivalentOf("reproducibility gate");
      (await File.ReadAllTextAsync(output)).Should().Be(string.Empty);
    }
    finally
    {
      Directory.Delete(fixture, recursive: true);
    }
  }

  [Fact]
  public async Task StagedIndexVerifierPublishesOnlyValidatedPlatformDigests()
  {
    var fixture = Path.Combine(Path.GetTempPath(), $"hevy-staged-{Guid.NewGuid():N}");
    Directory.CreateDirectory(fixture);
    try
    {
      var index = Path.Combine(fixture, "index.json");
      var bytes = CreatePlatformIndex(extraDescriptor: false, unknownDescriptor: false);
      await File.WriteAllBytesAsync(index, bytes);
      var digest = Sha256Digest(bytes);
      var output = Path.Combine(fixture, "github-output.txt");

      var result = await RunScriptAsync(
          Path.Combine(RepositoryRoot, "scripts", "verify-staged-index.sh"),
          [index, digest, digest, ExistingDigest, IntendedDigest],
          new Dictionary<string, string?> { ["GITHUB_OUTPUT"] = output });

      (result.ExitCode).Should().Be(0);
      (await File.ReadAllTextAsync(output)).Should().Be($"amd64_digest={ExistingDigest}\narm64_digest={IntendedDigest}\n");
    }
    finally
    {
      Directory.Delete(fixture, recursive: true);
    }
  }

  [Theory]
  [InlineData("install-syft.sh", "https://github.com/anchore/syft/releases/download/v1.49.0/syft_1.49.0_linux_amd64.tar.gz", "7aa2f03ee92739cf643279ba3990548b9925d4e22cae13f46831ee62821147fe")]
  [InlineData("install-buildx.sh", "https://github.com/docker/buildx/releases/download/v0.35.0/buildx-v0.35.0.linux-amd64", "d41ece72044243b4f58b343441ae37446d9c29a7d6b5e11c61847bbcf8f7dfda")]
  public async Task ToolInstallersUseExactDownloadAndChecksumAndCleanStaging(
      string scriptName,
      string expectedUrl,
      string expectedChecksum)
  {
    var fixture = await InstallerFixture.CreateAsync(scriptName, expectedUrl, expectedChecksum);
    try
    {
      var result = await fixture.RunAsync(checksumSucceeds: true);

      (result.ExitCode).Should().Be(0);
      (await File.ReadAllTextAsync(fixture.UrlLog)).Should().Be(expectedUrl + "\n");
      (await File.ReadAllTextAsync(fixture.ChecksumLog)).Should().Be(expectedChecksum + "\n");
      (Directory.GetDirectories(fixture.RunnerTemp)).Should().NotContain((static path => Path.GetFileName(path).StartsWith("hevy-syft.", StringComparison.Ordinal) ||
              Path.GetFileName(path).StartsWith("hevy-buildx.", StringComparison.Ordinal)));
      (File.Exists(fixture.InstalledExecutable)).Should().BeTrue();
      if (scriptName == "install-buildx.sh")
      {
        (File.Exists(fixture.GitHubEnvironment)).Should().BeFalse();
        (await File.ReadAllTextAsync(fixture.GitHubOutput)).Should().Be($"buildx_path={fixture.InstalledExecutable}\n");
      }
    }
    finally
    {
      fixture.Dispose();
    }
  }

  [Theory]
  [InlineData("install-syft.sh", "https://github.com/anchore/syft/releases/download/v1.49.0/syft_1.49.0_linux_amd64.tar.gz", "7aa2f03ee92739cf643279ba3990548b9925d4e22cae13f46831ee62821147fe")]
  [InlineData("install-buildx.sh", "https://github.com/docker/buildx/releases/download/v0.35.0/buildx-v0.35.0.linux-amd64", "d41ece72044243b4f58b343441ae37446d9c29a7d6b5e11c61847bbcf8f7dfda")]
  public async Task ToolInstallersFailClosedOnChecksumMismatchAndCleanStaging(
      string scriptName,
      string expectedUrl,
      string expectedChecksum)
  {
    var fixture = await InstallerFixture.CreateAsync(scriptName, expectedUrl, expectedChecksum);
    try
    {
      var result = await fixture.RunAsync(checksumSucceeds: false);

      (result.ExitCode).Should().NotBe(0);
      (Directory.GetDirectories(fixture.RunnerTemp)).Should().NotContain((static path => Path.GetFileName(path).StartsWith("hevy-syft.", StringComparison.Ordinal) ||
              Path.GetFileName(path).StartsWith("hevy-buildx.", StringComparison.Ordinal)));
      (File.Exists(fixture.InstalledExecutable)).Should().BeFalse();
    }
    finally
    {
      fixture.Dispose();
    }
  }

  [Theory]
  [InlineData("github.com/docker/buildx v0.35.0 a319e5b15052cf6557ceb666eb8ff6e32380b782", 0)]
  [InlineData("github.com/docker/buildx v0.35.0 wrong", 1)]
  public async Task BuildxVersionVerifierRequiresExactVersionAndCommit(string reportedVersion, int expectedSuccess)
  {
    var fixture = Path.Combine(Path.GetTempPath(), $"hevy-buildx-version-{Guid.NewGuid():N}");
    Directory.CreateDirectory(fixture);
    try
    {
      var fakeBuildx = Path.Combine(fixture, "buildx");
      await File.WriteAllTextAsync(fakeBuildx, $"#!/bin/sh\nprintf '%s\\n' '{reportedVersion}'\n");
      MakeExecutable(fakeBuildx);
      var result = await RunScriptAsync(
          Path.Combine(RepositoryRoot, "scripts", "verify-buildx-version.sh"),
          [],
          new Dictionary<string, string?> { ["HEVY_BUILDX_PATH"] = fakeBuildx });

      (result.ExitCode == 0 ? 0 : 1).Should().Be(expectedSuccess == 0 ? 0 : 1);
    }
    finally
    {
      Directory.Delete(fixture, recursive: true);
    }
  }

  [Theory]
  [InlineData($"present {IntendedDigest}", 0, false)]
  [InlineData($"present {ExistingDigest}", 1, false)]
  [InlineData("absent", 23, true)]
  public async Task FinalPromotionIsIdempotentRejectsConflictsAndDelegatesTheOnlyWrite(
      string probeResult,
      int expectedExitCode,
      bool expectsDocker)
  {
    var source = Path.Combine(RepositoryRoot, "scripts", "promote-ghcr-tag.sh");
    (File.Exists(source)).Should().BeTrue("The executable final-promotion transaction must exist.");
    var fixture = Path.Combine(Path.GetTempPath(), $"hevy-promote-{Guid.NewGuid():N}");
    var scripts = Path.Combine(fixture, "scripts");
    var binaries = Path.Combine(fixture, "bin");
    Directory.CreateDirectory(scripts);
    Directory.CreateDirectory(binaries);
    try
    {
      var promotion = Path.Combine(scripts, "promote-ghcr-tag.sh");
      File.Copy(source, promotion);
      var validator = Path.Combine(scripts, "validate-sha256-digest.sh");
      File.Copy(Path.Combine(RepositoryRoot, "scripts", "validate-sha256-digest.sh"), validator);
      var probe = Path.Combine(scripts, "ghcr-manifest.sh");
      await File.WriteAllTextAsync(probe, "#!/bin/sh\nprintf '%s\\n' \"$PROBE_RESULT\"\n");
      var docker = Path.Combine(binaries, "docker");
      await File.WriteAllTextAsync(
          docker,
          "#!/bin/sh\nprintf '%s\\n' \"$@\" > \"$DOCKER_LOG\"\nexit \"$DOCKER_EXIT_CODE\"\n");
      MakeExecutable(promotion);
      MakeExecutable(validator);
      MakeExecutable(probe);
      MakeExecutable(docker);
      var dockerLog = Path.Combine(fixture, "docker-arguments.txt");
      var environment = new Dictionary<string, string?>
      {
        ["PATH"] = $"{binaries}{Path.PathSeparator}{Environment.GetEnvironmentVariable("PATH")}",
        ["PROBE_RESULT"] = probeResult,
        ["DOCKER_LOG"] = dockerLog,
        ["DOCKER_EXIT_CODE"] = expectsDocker ? "23" : "0",
      };

      var result = await RunScriptAsync(
          promotion,
          ["ghcr.io/example/hevy-client", "1.2.3", IntendedDigest],
          environment);

      (result.ExitCode).Should().Be(expectedExitCode);
      (File.Exists(dockerLog)).Should().Be(expectsDocker);
      if (expectsDocker)
      {
        (await File.ReadAllTextAsync(dockerLog)).Should().Be($"buildx\nimagetools\ncreate\n--tag\nghcr.io/example/hevy-client:1.2.3\nghcr.io/example/hevy-client@{IntendedDigest}\n");
      }
    }
    finally
    {
      Directory.Delete(fixture, recursive: true);
    }
  }

  private static ActionlintPin ReadActionlintPin()
  {
    using var document = JsonDocument.Parse(
        File.ReadAllText(Path.Combine(RepositoryRoot, ".github", "tools-lock.json")));
    var actionlint = document.RootElement.GetProperty("tools").GetProperty("actionlint");
    return new ActionlintPin(
        actionlint.GetProperty("version").GetString()!,
        actionlint.GetProperty("archive").GetString()!,
        actionlint.GetProperty("sha256").GetString()!);
  }

  private sealed record ActionlintPin(string Version, string Archive, string Checksum);

  private static byte[] CreatePlatformIndex(
      bool extraDescriptor,
      bool unknownDescriptor,
      string arm64Digest = IntendedDigest,
      string arm64Architecture = "arm64",
      double amd64Size = 1,
      double arm64Size = 1)
  {
    var descriptors = new List<object>
    {
      new
      {
        mediaType = "application/vnd.oci.image.manifest.v1+json",
        digest = ExistingDigest,
        size = amd64Size,
        platform = new { os = "linux", architecture = "amd64" },
      },
      new
      {
        mediaType = "application/vnd.oci.image.manifest.v1+json",
        digest = arm64Digest,
        size = arm64Size,
        platform = new { os = "linux", architecture = arm64Architecture },
      },
    };
    if (extraDescriptor || unknownDescriptor)
    {
      descriptors.Add(new
      {
        mediaType = "application/vnd.oci.image.manifest.v1+json",
        digest = "sha256:cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
        size = 1,
        platform = unknownDescriptor
            ? new { os = "unknown", architecture = "unknown" }
            : new { os = "linux", architecture = "s390x" },
      });
    }

    return JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 2, manifests = descriptors });
  }

  private static byte[] CreateAdversarialPlatformIndex(string scenario)
  {
    var valid = CreatePlatformIndex(extraDescriptor: false, unknownDescriptor: false);
    return scenario switch
    {
      "valid" => valid,
      "extra" => CreatePlatformIndex(extraDescriptor: true, unknownDescriptor: false),
      "unknown" => CreatePlatformIndex(extraDescriptor: false, unknownDescriptor: true),
      "concatenated_roots" => [.. valid, .. valid],
      "trailing_json" => [.. valid, .. Encoding.UTF8.GetBytes("\n{}")],
      "trailing_garbage" => [.. valid, .. Encoding.UTF8.GetBytes("\nnot-json")],
      "newline_digest" => CreatePlatformIndex(
          extraDescriptor: false,
          unknownDescriptor: false,
          arm64Digest: IntendedDigest + "\n"),
      "duplicate_amd64" => CreatePlatformIndex(
          extraDescriptor: false,
          unknownDescriptor: false,
          arm64Architecture: "amd64"),
      "fractional_size" => CreatePlatformIndex(
          extraDescriptor: false,
          unknownDescriptor: false,
          amd64Size: 1.5),
      "large_fractional_size" => Encoding.UTF8.GetBytes(
          Encoding.UTF8.GetString(valid).Replace(
              "\"size\":1",
              "\"size\":9223372036854775807.5",
              StringComparison.Ordinal)),
      "string_schema" => Encoding.UTF8.GetBytes(
          Encoding.UTF8.GetString(valid).Replace(
              "\"schemaVersion\":2",
              "\"schemaVersion\":\"2\"",
              StringComparison.Ordinal)),
      "zero_size" => CreatePlatformIndex(
          extraDescriptor: false,
          unknownDescriptor: false,
          amd64Size: 0),
      "negative_size" => CreatePlatformIndex(
          extraDescriptor: false,
          unknownDescriptor: false,
          amd64Size: -1),
      "max_size" => ReplaceDescriptorSizes(valid, "9223372036854775807"),
      "too_large_size" => ReplaceDescriptorSizes(valid, "9223372036854775808"),
      _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown OCI index scenario."),
    };
  }

  private static byte[] ReplaceDescriptorSizes(byte[] document, string rawSizeToken) =>
      Encoding.UTF8.GetBytes(
          Encoding.UTF8.GetString(document).Replace(
              "\"size\":1",
              $"\"size\":{rawSizeToken}",
              StringComparison.Ordinal));

  private static byte[] PadJsonDocument(byte[] document, int byteCount)
  {
    (document.Length <= byteCount).Should().BeTrue();
    var padded = new byte[byteCount];
    document.CopyTo(padded, 0);
    padded.AsSpan(document.Length).Fill((byte)' ');
    return padded;
  }

  private static string Sha256Digest(byte[] value) =>
      $"sha256:{Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant()}";

  private sealed class ReproducibilityFixture : IDisposable
  {
    private const string Revision = "dddddddddddddddddddddddddddddddddddddddd";
    private readonly string root;
    private readonly string buildx;

    private ReproducibilityFixture(
        string root,
        string buildx,
        string buildLog,
        string githubOutput,
        string temporaryRoot,
        string indexDigest)
    {
      this.root = root;
      this.buildx = buildx;
      BuildLog = buildLog;
      GitHubOutput = githubOutput;
      TemporaryRoot = temporaryRoot;
      IndexDigest = indexDigest;
      var repositoryRoot = RepositoryRoot;
      var oneBuild = $"BEGIN\nSOURCE_DATE_EPOCH=1770000000\nbuild\n--platform\nlinux/amd64,linux/arm64\n--pull\n--no-cache\n--build-arg\nVERSION=1.2.3\n--build-arg\nREVISION={Revision}\n--build-arg\nSOURCE_URL=https://github.com/example/hevy-client\n--provenance=false\n--sbom=false\n--output\ntype=oci,dest=ARCHIVE,rewrite-timestamp=true,compatibility-version=30,oci-mediatypes=true\n{repositoryRoot}\nEND\n";
      ExpectedBuildLog = oneBuild.Replace("ARCHIVE", "1", StringComparison.Ordinal) +
          oneBuild.Replace("ARCHIVE", "2", StringComparison.Ordinal);
    }

    public string BuildLog { get; }
    public string ExpectedBuildLog { get; }
    public string GitHubOutput { get; }
    public string IndexDigest { get; }
    public string TemporaryRoot { get; }

    public static async Task<ReproducibilityFixture> CreateAsync(
        bool mismatch,
        bool extraDescriptor,
        string? invalidIndexScenario = null,
        bool invalidOuterSize = false,
        bool invalidOuterSchema = false,
        string? outerSizeToken = null,
        int? outerDocumentSize = null)
    {
      var root = Path.Combine(Path.GetTempPath(), $"hevy-repro-{Guid.NewGuid():N}");
      var temporaryRoot = Path.Combine(root, "tmp");
      Directory.CreateDirectory(temporaryRoot);
      var firstArchive = Path.Combine(root, "first.tar");
      var secondArchive = Path.Combine(root, "second.tar");
      var firstIndex = invalidIndexScenario is null
          ? CreatePlatformIndex(extraDescriptor, unknownDescriptor: false)
          : CreateAdversarialPlatformIndex(invalidIndexScenario);
      var secondIndex = invalidIndexScenario is null
          ? CreatePlatformIndex(
              extraDescriptor: false,
              unknownDescriptor: false,
              arm64Digest: mismatch
                  ? "sha256:eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee"
                  : IntendedDigest)
          : firstIndex;
      await CreateOciArchiveAsync(
          root,
          "layout-1",
          firstArchive,
          firstIndex,
          invalidOuterSize,
          invalidOuterSchema,
          outerSizeToken,
          outerDocumentSize);
      await CreateOciArchiveAsync(
          root,
          "layout-2",
          secondArchive,
          secondIndex,
          invalidOuterSize,
          invalidOuterSchema,
          outerSizeToken,
          outerDocumentSize);
      var buildLog = Path.Combine(root, "build.log");
      var githubOutput = Path.Combine(root, "github-output.txt");
      var state = Path.Combine(root, "state.txt");
      await File.WriteAllTextAsync(githubOutput, "sentinel=preserve\n");
      await File.WriteAllTextAsync(state, "0\n");
      var buildx = Path.Combine(root, "buildx");
      await File.WriteAllTextAsync(
          buildx,
          """
          #!/bin/sh
          set -eu
          count=$(cat "$FAKE_BUILDX_STATE")
          count=$((count + 1))
          printf '%s\n' "$count" > "$FAKE_BUILDX_STATE"
          {
            printf 'BEGIN\nSOURCE_DATE_EPOCH=%s\n' "$SOURCE_DATE_EPOCH"
            output=
            want_output=false
            for argument do
              if [ "$want_output" = true ]; then
                output=$argument
                destination=${argument#type=oci,dest=}
                destination=${destination%%,*}
                printf 'type=oci,dest=%s%s\n' "$count" "${argument#*${destination}}"
                want_output=false
              else
                printf '%s\n' "$argument"
                if [ "$argument" = "--output" ]; then want_output=true; fi
              fi
            done
            printf 'END\n'
          } >> "$FAKE_BUILDX_LOG"
          if [ "${FAKE_BUILDX_FAIL_ON:-}" = "$count" ]; then exit 42; fi
          if [ "$count" = 1 ]; then cp "$FAKE_OCI_FIRST" "$destination"; else cp "$FAKE_OCI_SECOND" "$destination"; fi
          """);
      MakeExecutable(buildx);
      return new ReproducibilityFixture(
          root,
          buildx,
          buildLog,
          githubOutput,
          temporaryRoot,
          Sha256Digest(firstIndex));
    }

    public Task<DeliveryContractTests.ProcessResult> RunAsync(string? failOnBuild = null) =>
        RunScriptAsync(
            Path.Combine(RepositoryRoot, "scripts", "verify-reproducible-image.sh"),
            [],
            new Dictionary<string, string?>
            {
              ["FAKE_BUILDX_FAIL_ON"] = failOnBuild,
              ["FAKE_BUILDX_LOG"] = BuildLog,
              ["FAKE_BUILDX_STATE"] = Path.Combine(root, "state.txt"),
              ["FAKE_OCI_FIRST"] = Path.Combine(root, "first.tar"),
              ["FAKE_OCI_SECOND"] = Path.Combine(root, "second.tar"),
              ["GITHUB_OUTPUT"] = GitHubOutput,
              ["HEVY_BUILDX_PATH"] = buildx,
              ["REVISION"] = Revision,
              ["SOURCE_DATE_EPOCH"] = "1770000000",
              ["SOURCE_URL"] = "https://github.com/example/hevy-client",
              ["TMPDIR"] = TemporaryRoot,
              ["VERSION"] = "1.2.3",
            });

    public void Dispose() => Directory.Delete(root, recursive: true);

    private static async Task CreateOciArchiveAsync(
        string root,
        string layoutName,
        string archive,
        byte[] platformIndex,
        bool invalidOuterSize,
        bool invalidOuterSchema,
        string? outerSizeToken,
        int? outerDocumentSize)
    {
      var layout = Path.Combine(root, layoutName);
      var digest = Sha256Digest(platformIndex);
      var blobs = Path.Combine(layout, "blobs", "sha256");
      Directory.CreateDirectory(blobs);
      await File.WriteAllBytesAsync(Path.Combine(blobs, digest[7..]), platformIndex);
      await File.WriteAllTextAsync(Path.Combine(layout, "oci-layout"), "{\"imageLayoutVersion\":\"1.0.0\"}\n");
      var outerIndex = JsonSerializer.SerializeToUtf8Bytes(new
      {
        schemaVersion = 2,
        manifests = new[]
        {
          new
          {
            mediaType = "application/vnd.oci.image.index.v1+json",
            digest,
            size = platformIndex.Length,
          },
        },
      });
      if (invalidOuterSize)
      {
        outerIndex = Encoding.UTF8.GetBytes(
            Encoding.UTF8.GetString(outerIndex).Replace(
                $"\"size\":{platformIndex.Length}",
                "\"size\":9223372036854775807.5",
                StringComparison.Ordinal));
      }
      if (invalidOuterSchema)
      {
        outerIndex = Encoding.UTF8.GetBytes(
            Encoding.UTF8.GetString(outerIndex).Replace(
                "\"schemaVersion\":2",
                "\"schemaVersion\":\"2\"",
                StringComparison.Ordinal));
      }
      if (outerSizeToken is not null)
      {
        outerIndex = Encoding.UTF8.GetBytes(
            Encoding.UTF8.GetString(outerIndex).Replace(
                $"\"size\":{platformIndex.Length}",
                $"\"size\":{outerSizeToken}",
                StringComparison.Ordinal));
      }
      if (outerDocumentSize is not null)
      {
        outerIndex = PadJsonDocument(outerIndex, outerDocumentSize.Value);
      }
      await File.WriteAllBytesAsync(Path.Combine(layout, "index.json"), outerIndex);
      TarFile.CreateFromDirectory(layout, archive, includeBaseDirectory: false);
      Directory.Delete(layout, recursive: true);
    }
  }

  private sealed class ActionlintFixture : IDisposable
  {
    private readonly string root;

    private ActionlintFixture(string root)
    {
      this.root = root;
      TemporaryRoot = Path.Combine(root, "tmp");
      DownloadLog = Path.Combine(root, "download.log");
      ChecksumLog = Path.Combine(root, "checksum.log");
      ExecutionLog = Path.Combine(root, "execution.log");
    }

    public string TemporaryRoot { get; }
    public string DownloadLog { get; }
    public string ChecksumLog { get; }
    public string ExecutionLog { get; }

    public static async Task<ActionlintFixture> CreateAsync(bool toolPresent)
    {
      var root = Path.Combine(Path.GetTempPath(), $"hevy-actionlint-{Guid.NewGuid():N}");
      var temporaryRoot = Path.Combine(root, "tmp");
      var contents = Path.Combine(root, "contents");
      var binaries = Path.Combine(root, "bin");
      Directory.CreateDirectory(temporaryRoot);
      Directory.CreateDirectory(contents);
      Directory.CreateDirectory(binaries);
      var archivedName = toolPresent ? "actionlint" : "not-actionlint";
      var archivedTool = Path.Combine(contents, archivedName);
      await File.WriteAllTextAsync(
          archivedTool,
          toolPresent
              ? "#!/bin/sh\nprintf '%s\\n' \"$@\" > \"$ACTIONLINT_EXECUTION_LOG\"\nexit \"$ACTIONLINT_EXIT_CODE\"\n"
              : "missing tool fixture\n");
      if (toolPresent)
      {
        MakeExecutable(archivedTool);
      }
      var asset = Path.Combine(root, "actionlint.tar.gz");
      var tarResult = await DeliveryContractTests.RunProcessAsync(
          root,
          "tar",
          "-czf",
          asset,
          "-C",
          contents,
          archivedName);
      (tarResult.ExitCode).Should().Be(0);

      var curl = Path.Combine(binaries, "curl");
      await File.WriteAllTextAsync(
          curl,
          """
          #!/bin/sh
          set -eu
          output=
          : > "$ACTIONLINT_DOWNLOAD_LOG"
          while [ "$#" -gt 0 ]; do
            case "$1" in
              --output)
                output=$2
                printf '%s\n%s\n' "$1" "${2##*/}" >> "$ACTIONLINT_DOWNLOAD_LOG"
                shift 2
                ;;
              *)
                printf '%s\n' "$1" >> "$ACTIONLINT_DOWNLOAD_LOG"
                shift
                ;;
            esac
          done
          test "$ACTIONLINT_DOWNLOAD_SUCCEEDS" = true
          cp "$ACTIONLINT_ASSET" "$output"
          """);
      var checksum = Path.Combine(binaries, "sha256sum");
      await File.WriteAllTextAsync(
          checksum,
          """
          #!/bin/sh
          set -eu
          arguments=$*
          read -r checksum file
          printf 'arguments=%s\nchecksum=%s\nfile=%s\n' "$arguments" "$checksum" "${file##*/}" > "$ACTIONLINT_CHECKSUM_LOG"
          test "$ACTIONLINT_CHECKSUM_SUCCEEDS" = true
          """);
      MakeExecutable(curl);
      MakeExecutable(checksum);
      return new ActionlintFixture(root);
    }

    public Task<DeliveryContractTests.ProcessResult> RunAsync(
        bool downloadSucceeds,
        bool checksumSucceeds,
        int actionlintExitCode) =>
        RunScriptAsync(
            Path.Combine(RepositoryRoot, "scripts", "run-actionlint.sh"),
            [],
            new Dictionary<string, string?>
            {
              ["ACTIONLINT_ASSET"] = Path.Combine(root, "actionlint.tar.gz"),
              ["ACTIONLINT_CHECKSUM_LOG"] = ChecksumLog,
              ["ACTIONLINT_CHECKSUM_SUCCEEDS"] = checksumSucceeds ? "true" : "false",
              ["ACTIONLINT_DOWNLOAD_LOG"] = DownloadLog,
              ["ACTIONLINT_DOWNLOAD_SUCCEEDS"] = downloadSucceeds ? "true" : "false",
              ["ACTIONLINT_EXECUTION_LOG"] = ExecutionLog,
              ["ACTIONLINT_EXIT_CODE"] = actionlintExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
              ["HEVY_CURL_PATH"] = Path.Combine(root, "bin", "curl"),
              ["HEVY_SHA256SUM_PATH"] = Path.Combine(root, "bin", "sha256sum"),
              ["TMPDIR"] = TemporaryRoot,
            });

    public void Dispose() => Directory.Delete(root, recursive: true);
  }

  private sealed class InstallerFixture : IDisposable
  {
    private readonly string root;
    private readonly string scriptName;
    private readonly string expectedUrl;
    private readonly string expectedChecksum;

    private InstallerFixture(
        string root,
        string scriptName,
        string expectedUrl,
        string expectedChecksum)
    {
      this.root = root;
      this.scriptName = scriptName;
      this.expectedUrl = expectedUrl;
      this.expectedChecksum = expectedChecksum;
      RunnerTemp = Path.Combine(root, "runner-temp");
      UrlLog = Path.Combine(root, "url.log");
      ChecksumLog = Path.Combine(root, "checksum.log");
      GitHubEnvironment = Path.Combine(root, "github-env.txt");
      GitHubOutput = Path.Combine(root, "github-output.txt");
      DockerConfig = Path.Combine(root, "docker-config");
    }

    public string RunnerTemp { get; }
    public string UrlLog { get; }
    public string ChecksumLog { get; }
    public string GitHubEnvironment { get; }
    public string GitHubOutput { get; }
    public string DockerConfig { get; }
    public string InstalledExecutable => scriptName == "install-syft.sh"
        ? Path.Combine(RunnerTemp, "hevy-syft-bin", "syft")
        : Path.Combine(DockerConfig, "cli-plugins", "docker-buildx");

    public static async Task<InstallerFixture> CreateAsync(
        string scriptName,
        string expectedUrl,
        string expectedChecksum)
    {
      var root = Path.Combine(Path.GetTempPath(), $"hevy-installer-{Guid.NewGuid():N}");
      var runnerTemp = Path.Combine(root, "runner-temp");
      var bin = Path.Combine(root, "bin");
      Directory.CreateDirectory(runnerTemp);
      Directory.CreateDirectory(bin);
      var asset = Path.Combine(root, "asset");
      if (scriptName == "install-syft.sh")
      {
        var contents = Path.Combine(root, "syft-contents");
        Directory.CreateDirectory(contents);
        var syft = Path.Combine(contents, "syft");
        await File.WriteAllTextAsync(syft, "#!/bin/sh\nprintf 'syft fixture\\n'\n");
        MakeExecutable(syft);
        var tarResult = await DeliveryContractTests.RunProcessAsync(root, "tar", "-czf", asset, "-C", contents, "syft");
        (tarResult.ExitCode).Should().Be(0);
      }
      else
      {
        await File.WriteAllTextAsync(
            asset,
            "#!/bin/sh\nprintf '%s\\n' 'github.com/docker/buildx v0.35.0 a319e5b15052cf6557ceb666eb8ff6e32380b782'\n");
        MakeExecutable(asset);
      }

      var curl = Path.Combine(bin, "curl");
      await File.WriteAllTextAsync(
          curl,
          """
          #!/bin/sh
          set -eu
          output=
          url=
          while [ "$#" -gt 0 ]; do
            case "$1" in
              --output) output=$2; shift 2 ;;
              --*) shift ;;
              *) url=$1; shift ;;
            esac
          done
          printf '%s\n' "$url" > "$FAKE_URL_LOG"
          test "$url" = "$FAKE_EXPECTED_URL"
          cp "$FAKE_ASSET" "$output"
          """);
      var sha256sum = Path.Combine(bin, "sha256sum");
      await File.WriteAllTextAsync(
          sha256sum,
          """
          #!/bin/sh
          set -eu
          read -r checksum file
          printf '%s\n' "$checksum" > "$FAKE_CHECKSUM_LOG"
          test "$checksum" = "$FAKE_EXPECTED_CHECKSUM"
          test -f "$file"
          test "$FAKE_CHECKSUM_SUCCEEDS" = true
          """);
      MakeExecutable(curl);
      MakeExecutable(sha256sum);
      return new InstallerFixture(root, scriptName, expectedUrl, expectedChecksum);
    }

    public Task<DeliveryContractTests.ProcessResult> RunAsync(bool checksumSucceeds)
    {
      var githubPath = Path.Combine(root, "github-path.txt");
      return RunScriptAsync(
          Path.Combine(RepositoryRoot, "scripts", scriptName),
          [],
          new Dictionary<string, string?>
          {
            ["FAKE_ASSET"] = Path.Combine(root, "asset"),
            ["FAKE_CHECKSUM_LOG"] = ChecksumLog,
            ["FAKE_CHECKSUM_SUCCEEDS"] = checksumSucceeds ? "true" : "false",
            ["FAKE_EXPECTED_CHECKSUM"] = expectedChecksum,
            ["FAKE_EXPECTED_URL"] = expectedUrl,
            ["FAKE_URL_LOG"] = UrlLog,
            ["GITHUB_ENV"] = GitHubEnvironment,
            ["GITHUB_OUTPUT"] = GitHubOutput,
            ["GITHUB_PATH"] = githubPath,
            ["HEVY_CURL_PATH"] = Path.Combine(root, "bin", "curl"),
            ["HEVY_DOCKER_CONFIG"] = DockerConfig,
            ["HEVY_SHA256SUM_PATH"] = Path.Combine(root, "bin", "sha256sum"),
            ["RUNNER_TEMP"] = RunnerTemp,
          });
    }

    public void Dispose() => Directory.Delete(root, recursive: true);
  }

  private static string GhcrProbeScript()
  {
    var script = Path.Combine(RepositoryRoot, "scripts", "ghcr-manifest.sh");
    (File.Exists(script)).Should().BeTrue("The executable authenticated GHCR manifest probe must exist.");
    return script;
  }

  private static Task<DeliveryContractTests.ProcessResult> RunScriptAsync(
      string script,
      IReadOnlyList<string> arguments,
      IReadOnlyDictionary<string, string?>? environment) =>
      DeliveryContractTests.RunProcessAsync(RepositoryRoot, script, environment, arguments.ToArray());

  private static bool HasBasicCredentials(RegistryRequest request, string actor, string secret)
  {
    if (!request.Headers.TryGetValue("Authorization", out var authorization) ||
        !authorization.StartsWith("Basic ", StringComparison.Ordinal))
    {
      return false;
    }

    try
    {
      var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(authorization[6..]));
      return string.Equals(decoded, $"{actor}:{secret}", StringComparison.Ordinal);
    }
    catch (FormatException)
    {
      return false;
    }
  }

  private static void MakeExecutable(string path)
  {
    if (OperatingSystem.IsWindows())
    {
      return;
    }

    File.SetUnixFileMode(
        path,
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
  }

  private sealed class CurlFixture : IAsyncDisposable
  {
    private readonly string path;

    private CurlFixture(string path, string argumentLog)
    {
      this.path = path;
      ArgumentLog = argumentLog;
    }

    public string ArgumentLog { get; }

    public static async Task<CurlFixture> CreateAsync()
    {
      var path = Path.Combine(Path.GetTempPath(), $"hevy-curl-{Guid.NewGuid():N}");
      var binaries = Path.Combine(path, "bin");
      Directory.CreateDirectory(binaries);
      var argumentLog = Path.Combine(path, "arguments.txt");
      await File.WriteAllTextAsync(argumentLog, string.Empty);
      var wrapper = Path.Combine(binaries, "curl");
      await File.WriteAllTextAsync(
          wrapper,
          "#!/bin/sh\nfor argument do printf '%s\\n' \"$argument\" >> \"$CURL_ARGS_LOG\"; done\nexec \"$REAL_CURL\" \"$@\"\n");
      MakeExecutable(wrapper);
      return new CurlFixture(path, argumentLog);
    }

    public IReadOnlyDictionary<string, string?> Environment(Uri registry, string secret) =>
        new Dictionary<string, string?>
        {
          ["PATH"] = $"{Path.Combine(path, "bin")}{Path.PathSeparator}{System.Environment.GetEnvironmentVariable("PATH")}",
          ["REAL_CURL"] = FindExecutable("curl"),
          ["CURL_ARGS_LOG"] = ArgumentLog,
          ["GHCR_AUTH_TESTING"] = "true",
          ["GHCR_REGISTRY_BASE"] = registry.GetLeftPart(UriPartial.Authority),
          ["GITHUB_ACTOR"] = "release-actor",
          ["GHCR_TOKEN"] = secret,
        };

    public ValueTask DisposeAsync()
    {
      Directory.Delete(path, recursive: true);
      return ValueTask.CompletedTask;
    }

    private static string FindExecutable(string name)
    {
      foreach (var directory in (System.Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator))
      {
        var candidate = Path.Combine(directory, name);
        if (File.Exists(candidate))
        {
          return candidate;
        }
      }

      throw new InvalidOperationException($"Required executable was not found: {name}");
    }
  }

  private sealed class RegistryFixture : IAsyncDisposable
  {
    private readonly CancellationTokenSource cancellation = new();
    private readonly TcpListener listener;
    private readonly RegistryScenario scenario;
    private readonly Task server;

    private RegistryFixture(RegistryScenario scenario)
    {
      this.scenario = scenario;
      listener = new TcpListener(IPAddress.Loopback, 0);
      listener.Start();
      var endpoint = (IPEndPoint)listener.LocalEndpoint;
      BaseUri = new Uri($"http://127.0.0.1:{endpoint.Port}");
      server = ServeAsync();
    }

    public Uri BaseUri { get; }

    public ConcurrentQueue<RegistryRequest> Requests { get; } = new();

    public static RegistryFixture Start(RegistryScenario scenario) => new(scenario);

    public async ValueTask DisposeAsync()
    {
      cancellation.Cancel();
      listener.Stop();
      try
      {
        await server;
      }
      catch (Exception exception) when (exception is OperationCanceledException or SocketException)
      {
      }
      cancellation.Dispose();
    }

    private async Task ServeAsync()
    {
      var requestNumber = 0;
      while (!cancellation.IsCancellationRequested)
      {
        using var client = await listener.AcceptTcpClientAsync(cancellation.Token);
        await using var stream = client.GetStream();
        var request = await ReadRequestAsync(stream, cancellation.Token);
        Requests.Enqueue(request);
        requestNumber++;
        if (requestNumber == 1)
        {
          var tokenRealm = $"{BaseUri.GetLeftPart(UriPartial.Authority)}/token";
          var challenge = scenario.MalformedChallenge
              ? $"Basic realm=\"{tokenRealm}\""
              : scenario.ReorderedChallenge
                  ? $"Bearer scope=\"repository:example/hevy-client:pull\",nonce=\"safe-extension\",realm=\"{tokenRealm}\",service=\"{BaseUri.Authority}\""
                  : scenario.DuplicateScope
                      ? $"Bearer realm=\"{tokenRealm}\",scope=\"repository:example/hevy-client:pull\",service=\"{BaseUri.Authority}\",scope=\"repository:example/hevy-client:pull\""
                      : $"Bearer realm=\"{tokenRealm}\",service=\"{BaseUri.Authority}\",scope=\"repository:example/hevy-client:pull\"";
          await RespondAsync(
              stream,
              401,
              string.Empty,
              [("WWW-Authenticate", challenge)],
              cancellation.Token,
              declaredContentLength: 73);
        }
        else if (requestNumber == 2)
        {
          var body = scenario.MalformedToken
              ? "{\"token\":\"contains a space\"}"
              : "{\"token\":\"fixture-registry-bearer-token\"}";
          await RespondAsync(stream, scenario.TokenStatus, body, [], cancellation.Token);
        }
        else
        {
          var headers = scenario.AuthenticatedStatus == 200
              ? new[] { ("Docker-Content-Digest", ExistingDigest) }
              : [];
          await RespondAsync(stream, scenario.AuthenticatedStatus, string.Empty, headers, cancellation.Token);
        }
      }
    }

    private static async Task<RegistryRequest> ReadRequestAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
      using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
      var requestLine = await reader.ReadLineAsync(cancellationToken) ?? string.Empty;
      var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      while (await reader.ReadLineAsync(cancellationToken) is { Length: > 0 } line)
      {
        var separator = line.IndexOf(':');
        if (separator > 0)
        {
          headers[line[..separator]] = line[(separator + 1)..].Trim();
        }
      }

      return new RegistryRequest(requestLine, headers);
    }

    private static async Task RespondAsync(
        NetworkStream stream,
        int status,
        string body,
        IReadOnlyList<(string Name, string Value)> headers,
        CancellationToken cancellationToken,
        int? declaredContentLength = null)
    {
      var reason = status switch
      {
        200 => "OK",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        418 => "I'm a Teapot",
        500 => "Internal Server Error",
        _ => "Status",
      };
      var bodyBytes = Encoding.UTF8.GetBytes(body);
      var response = new StringBuilder()
          .Append("HTTP/1.1 ").Append(status).Append(' ').Append(reason).Append("\r\n")
          .Append("Connection: close\r\n")
          .Append("Content-Type: application/json\r\n")
          .Append("Content-Length: ").Append(declaredContentLength ?? bodyBytes.Length).Append("\r\n");
      foreach (var header in headers)
      {
        response.Append(header.Name).Append(": ").Append(header.Value).Append("\r\n");
      }
      response.Append("\r\n");
      await stream.WriteAsync(Encoding.ASCII.GetBytes(response.ToString()), cancellationToken);
      if (bodyBytes.Length > 0)
      {
        await stream.WriteAsync(bodyBytes, cancellationToken);
      }
    }
  }

  private sealed record RegistryRequest(string RequestLine, IReadOnlyDictionary<string, string> Headers);

  private sealed record RegistryScenario(
      bool MalformedChallenge = false,
      int TokenStatus = 200,
      bool MalformedToken = false,
      int AuthenticatedStatus = 404,
      bool ReorderedChallenge = false,
      bool DuplicateScope = false);
}
