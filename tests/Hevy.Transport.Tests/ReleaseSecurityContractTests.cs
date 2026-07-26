using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Xunit;

namespace Hevy.Transport.Tests;

public sealed class ReleaseSecurityContractTests
{
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
    var secret = string.Concat("ghp_registry_", "fixture_7Qm4N2x9Vp6K8s3R5t1W");
    await using var registry = RegistryFixture.Start(new RegistryScenario(AuthenticatedStatus: authenticatedStatus));
    await using var fixture = await CurlFixture.CreateAsync();

    var result = await RunScriptAsync(
        script,
        ["ghcr.io/example/hevy-client", "1.2.3"],
        fixture.Environment(registry.BaseUri, secret));

    Assert.Equal(0, result.ExitCode);
    Assert.Equal(expectedOutput, result.StandardOutput.Trim());
    Assert.DoesNotContain(secret, result.StandardOutput, StringComparison.Ordinal);
    Assert.DoesNotContain(secret, result.StandardError, StringComparison.Ordinal);
    Assert.DoesNotContain(secret, await File.ReadAllTextAsync(fixture.ArgumentLog), StringComparison.Ordinal);

    var requests = registry.Requests.ToArray();
    Assert.Equal(3, requests.Length);
    Assert.False(requests[0].Headers.ContainsKey("Authorization"));
    Assert.True(
        HasBasicCredentials(requests[1], "release-actor", secret),
        "The scoped token request did not carry the expected credentials.");
    Assert.Equal("Bearer fixture-registry-bearer-token", requests[2].Headers["Authorization"]);
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

    Assert.NotEqual(0, result.ExitCode);
    Assert.Contains("authenticated manifest probe failed", result.StandardError, StringComparison.OrdinalIgnoreCase);
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

    Assert.NotEqual(0, result.ExitCode);
    Assert.DoesNotContain("fixture-registry-secret", result.StandardError, StringComparison.Ordinal);
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

    Assert.Equal(0, result.ExitCode);
    Assert.Equal("absent", result.StandardOutput.Trim());
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

    Assert.NotEqual(0, result.ExitCode);
    Assert.Contains("duplicate", result.StandardError, StringComparison.OrdinalIgnoreCase);
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

    Assert.NotEqual(0, result.ExitCode);
    Assert.Contains("registry challenge probe failed", result.StandardError, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public async Task SpdxValidatorRequiresVersion23AndEmitsItsExactPredicateType()
  {
    var script = Path.Combine(RepositoryRoot, "scripts", "validate-spdx.sh");
    Assert.True(File.Exists(script), "The executable SPDX validator must exist.");
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
      Assert.Equal(0, accepted.ExitCode);
      Assert.Equal("predicate_type=https://spdx.dev/Document/v2.3\n", await File.ReadAllTextAsync(output));

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
        Assert.NotEqual(0, rejected.ExitCode);
        Assert.Contains("complete SPDX-2.3", rejected.StandardError, StringComparison.Ordinal);
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
    Assert.True(File.Exists(script), "The executable SHA-256 digest validator must exist.");
    var accepted = await RunScriptAsync(script, [ExistingDigest, IntendedDigest], environment: null);
    Assert.Equal(0, accepted.ExitCode);

    var rejected = await RunScriptAsync(script, [ExistingDigest, "sha256:not-a-digest"], environment: null);
    Assert.NotEqual(0, rejected.ExitCode);
    Assert.Contains("SHA-256 digest", rejected.StandardError, StringComparison.Ordinal);
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
    Assert.True(File.Exists(source), "The executable final-promotion transaction must exist.");
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

      Assert.Equal(expectedExitCode, result.ExitCode);
      Assert.Equal(expectsDocker, File.Exists(dockerLog));
      if (expectsDocker)
      {
        Assert.Equal(
            $"buildx\nimagetools\ncreate\n--tag\nghcr.io/example/hevy-client:1.2.3\nghcr.io/example/hevy-client@{IntendedDigest}\n",
            await File.ReadAllTextAsync(dockerLog));
      }
    }
    finally
    {
      Directory.Delete(fixture, recursive: true);
    }
  }

  private static string GhcrProbeScript()
  {
    var script = Path.Combine(RepositoryRoot, "scripts", "ghcr-manifest.sh");
    Assert.True(File.Exists(script), "The executable authenticated GHCR manifest probe must exist.");
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
