using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Sdk;

namespace Hevy.Transport.Tests;

public enum DockerAvailabilityDecision
{
  Use,
  Skip,
  Fail,
}

public sealed record DockerProbeResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool ExecutableMissing);

public sealed record DockerCommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool ExecutableMissing);

public delegate Task<DockerCommandResult> DockerCommandRunner(
    IReadOnlyList<string> arguments,
    string? workingDirectory,
    TimeSpan? timeout);

public static class DockerAvailabilityPolicy
{
  private const string DockerEndpointFormat = "{{(index .Endpoints \"docker\").Host}}";

  /// <remarks>
  /// Docker CLI 29.5.2 resolves a nonempty <c>DOCKER_HOST</c> before <c>DOCKER_CONTEXT</c>.
  /// Re-audit this precedence when the pinned Docker client changes.
  /// </remarks>
  public static async Task<DockerAvailabilityDecision> EvaluateAsync(
      DockerProbeResult probe,
      bool isCi,
      DockerCommandRunner runner,
      string? configuredDockerHost = null,
      string? configuredDockerContext = null)
  {
    ArgumentNullException.ThrowIfNull(probe);
    ArgumentNullException.ThrowIfNull(runner);

    if (probe.ExitCode == 0)
    {
      return DockerAvailabilityDecision.Use;
    }

    if (isCi)
    {
      return DockerAvailabilityDecision.Fail;
    }

    if (probe.ExecutableMissing)
    {
      return DockerAvailabilityDecision.Skip;
    }

    var effectiveEndpoint = configuredDockerHost;
    if (string.IsNullOrEmpty(effectiveEndpoint))
    {
      try
      {
        var effectiveContext = configuredDockerContext;
        if (string.IsNullOrEmpty(effectiveContext))
        {
          var activeContext = await runner(
              ["context", "show"],
              workingDirectory: null,
              timeout: TimeSpan.FromSeconds(10));
          if (!TryReadCommandValue(activeContext, out effectiveContext))
          {
            return DockerAvailabilityDecision.Fail;
          }
        }

        if (!IsValidDockerContextName(effectiveContext))
        {
          return DockerAvailabilityDecision.Fail;
        }

        var inspection = await runner(
            ["context", "inspect", "--format", DockerEndpointFormat, "--", effectiveContext],
            workingDirectory: null,
            timeout: TimeSpan.FromSeconds(10));
        if (!TryReadCommandValue(inspection, out effectiveEndpoint))
        {
          return DockerAvailabilityDecision.Fail;
        }
      }
      catch (TimeoutException)
      {
        return DockerAvailabilityDecision.Fail;
      }
    }

    return IsLocalEndpoint(effectiveEndpoint) && IsRecognizedStoppedLocalDaemon(probe.StandardError)
        ? DockerAvailabilityDecision.Skip
        : DockerAvailabilityDecision.Fail;
  }

  private static bool TryReadCommandValue(DockerCommandResult result, out string value)
  {
    value = string.Empty;
    if (result.ExitCode != 0 || result.ExecutableMissing)
    {
      return false;
    }

    var output = result.StandardOutput;
    if (output.EndsWith("\r\n", StringComparison.Ordinal))
    {
      output = output[..^2];
    }
    else if (output.EndsWith('\n'))
    {
      output = output[..^1];
    }

    if (!IsSafeBoundedText(output, maximumLength: 2048))
    {
      return false;
    }

    value = output;
    return true;
  }

  private static bool IsRecognizedStoppedLocalDaemon(string error)
  {
    const int maximumDiagnosticLength = 4096;
    if (!IsSafeBoundedText(error, maximumDiagnosticLength))
    {
      return false;
    }

    var diagnostic = error;

    const string daemonPrefix = "Cannot connect to the Docker daemon at ";
    const string daemonSuffix = ". Is the docker daemon running?";
    if (diagnostic.StartsWith(daemonPrefix, StringComparison.Ordinal) &&
        diagnostic.EndsWith(daemonSuffix, StringComparison.Ordinal))
    {
      var endpoint = diagnostic[daemonPrefix.Length..^daemonSuffix.Length];
      return IsLocalEndpoint(endpoint);
    }

    return IsLocalWindowsPipeUnavailable(diagnostic) || IsLocalApiRefusal(diagnostic);
  }

  private static bool IsLocalWindowsPipeUnavailable(string diagnostic)
  {
    const string pattern = "^error during connect: " +
        "(?:in the default daemon configuration on Windows, the docker client must be run with elevated privileges to connect: )?" +
        "Get (?<quote>\")?http://%2F%2F\\.%2Fpipe%2F(?<requested>docker_engine|dockerDesktopLinuxEngine)(?:/[^\"\\s:]*)?(?(quote)\")" +
        ": open //\\./pipe/(?<opened>docker_engine|dockerDesktopLinuxEngine)" +
        ": The system cannot find the file specified\\.?$";
    var match = Regex.Match(
        diagnostic,
        pattern,
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        TimeSpan.FromMilliseconds(100));
    return match.Success && string.Equals(
        match.Groups["requested"].Value,
        match.Groups["opened"].Value,
        StringComparison.OrdinalIgnoreCase);
  }

  private static bool IsLocalApiRefusal(string diagnostic)
  {
    const string prefix = "failed to connect to the docker API at ";
    if (!diagnostic.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
        diagnostic[prefix.Length..].Contains(prefix, StringComparison.OrdinalIgnoreCase))
    {
      return false;
    }

    var remainder = diagnostic[prefix.Length..];
    const string detailedSeparator = "; check if the path is correct and if the daemon is running: ";
    var separatorIndex = remainder.IndexOf(detailedSeparator, StringComparison.OrdinalIgnoreCase);
    var separatorLength = detailedSeparator.Length;
    if (separatorIndex < 0)
    {
      separatorIndex = remainder.IndexOf(": ", StringComparison.Ordinal);
      separatorLength = 2;
    }
    if (separatorIndex <= 0)
    {
      return false;
    }

    var endpoint = remainder[..separatorIndex];
    var reason = remainder[(separatorIndex + separatorLength)..];
    if (!IsLocalEndpoint(endpoint) || !IsBoundConnectionRefusal(reason))
    {
      return false;
    }

    return true;
  }

  private static bool IsBoundConnectionRefusal(string reason)
  {
    if (string.Equals(reason, "connection refused", StringComparison.OrdinalIgnoreCase))
    {
      return true;
    }

    const string dialPrefix = "dial tcp ";
    const string dialSuffix = ": connect: connection refused";
    if (!reason.StartsWith(dialPrefix, StringComparison.OrdinalIgnoreCase) ||
        !reason.EndsWith(dialSuffix, StringComparison.OrdinalIgnoreCase))
    {
      return false;
    }

    var dialAddress = reason[dialPrefix.Length..^dialSuffix.Length];
    return IsLocalEndpoint($"tcp://{dialAddress}");
  }

  private static bool IsLocalEndpoint(string endpoint)
  {
    const int maximumEndpointLength = 2048;
    if (!IsSafeBoundedText(endpoint, maximumEndpointLength))
    {
      return false;
    }

    if (string.Equals(endpoint, "npipe:////./pipe/docker_engine", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(endpoint, "npipe:////./pipe/dockerDesktopLinuxEngine", StringComparison.OrdinalIgnoreCase))
    {
      return true;
    }

    const string unixPrefix = "unix:///";
    if (endpoint.StartsWith(unixPrefix, StringComparison.Ordinal))
    {
      return IsExactAbsoluteUnixSocketPath(endpoint.AsSpan(unixPrefix.Length));
    }

    ReadOnlySpan<char> authority;
    if (endpoint.StartsWith("tcp://", StringComparison.Ordinal))
    {
      authority = endpoint.AsSpan("tcp://".Length);
    }
    else if (endpoint.StartsWith("http://", StringComparison.Ordinal))
    {
      authority = endpoint.AsSpan("http://".Length);
    }
    else if (endpoint.StartsWith("https://", StringComparison.Ordinal))
    {
      authority = endpoint.AsSpan("https://".Length);
    }
    else
    {
      return false;
    }

    return IsExactLoopbackAuthority(authority);
  }

  private static bool IsExactAbsoluteUnixSocketPath(ReadOnlySpan<char> path)
  {
    if (path.IsEmpty || path[0] == '/' || path[^1] == '/')
    {
      return false;
    }

    var segmentStart = 0;
    for (var index = 0; index <= path.Length; index++)
    {
      if (index < path.Length && path[index] != '/')
      {
        var character = path[index];
        if (!IsAsciiLetterOrDigit(character) && character is not '.' and not '_' and not '-')
        {
          return false;
        }

        continue;
      }

      var segment = path[segmentStart..index];
      if (segment.IsEmpty || segment.SequenceEqual(".") || segment.SequenceEqual(".."))
      {
        return false;
      }

      segmentStart = index + 1;
    }

    return true;
  }

  private static bool IsExactLoopbackAuthority(ReadOnlySpan<char> authority)
  {
    if (authority.IsEmpty)
    {
      return false;
    }

    ReadOnlySpan<char> host;
    ReadOnlySpan<char> port;
    if (authority[0] == '[')
    {
      var closingBracket = authority.IndexOf(']');
      if (closingBracket <= 1 || closingBracket + 1 >= authority.Length || authority[closingBracket + 1] != ':')
      {
        return false;
      }

      host = authority[1..closingBracket];
      port = authority[(closingBracket + 2)..];
      if (host.Contains('%') || !IPAddress.TryParse(host, out var address) || !IPAddress.IsLoopback(address))
      {
        return false;
      }
    }
    else
    {
      var separator = authority.LastIndexOf(':');
      if (separator <= 0 || authority[..separator].Contains(':'))
      {
        return false;
      }

      host = authority[..separator];
      port = authority[(separator + 1)..];
      if (!host.Equals("localhost", StringComparison.OrdinalIgnoreCase) && !IsExactIpv4Loopback(host))
      {
        return false;
      }
    }

    return IsValidPort(port);
  }

  private static bool IsExactIpv4Loopback(ReadOnlySpan<char> host)
  {
    Span<int> octets = stackalloc int[4];
    var octetIndex = 0;
    var segmentStart = 0;
    for (var index = 0; index <= host.Length; index++)
    {
      if (index < host.Length && host[index] != '.')
      {
        continue;
      }

      if (octetIndex >= octets.Length || !TryParseDecimal(host[segmentStart..index], 255, out octets[octetIndex]))
      {
        return false;
      }

      octetIndex++;
      segmentStart = index + 1;
    }

    return octetIndex == octets.Length && octets[0] == 127;
  }

  private static bool IsValidPort(ReadOnlySpan<char> port) =>
      TryParseDecimal(port, ushort.MaxValue, out var value) && value > 0;

  private static bool TryParseDecimal(ReadOnlySpan<char> value, int maximum, out int parsed)
  {
    parsed = 0;
    if (value.IsEmpty || value.Length > 5 || (value.Length > 1 && value[0] == '0'))
    {
      return false;
    }

    foreach (var character in value)
    {
      if (character is < '0' or > '9')
      {
        return false;
      }

      parsed = (parsed * 10) + (character - '0');
      if (parsed > maximum)
      {
        return false;
      }
    }

    return true;
  }

  private static bool IsAsciiLetterOrDigit(char character) =>
      character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';

  private static bool IsValidDockerContextName(string value)
  {
    const int maximumContextNameLength = 256;
    if (value.Length is < 2 or > maximumContextNameLength || !IsAsciiLetterOrDigit(value[0]))
    {
      return false;
    }

    foreach (var character in value.AsSpan(1))
    {
      if (!IsAsciiLetterOrDigit(character) && character is not '_' and not '.' and not '+' and not '-')
      {
        return false;
      }
    }

    return true;
  }

  private static bool IsSafeBoundedText(string value, int maximumLength)
  {
    if (string.IsNullOrEmpty(value) || value.Length > maximumLength)
    {
      return false;
    }

    foreach (var character in value)
    {
      if (char.IsControl(character) ||
          char.GetUnicodeCategory(character) is UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator)
      {
        return false;
      }
    }

    return true;
  }
}

public sealed class ContainerImageCoordinator : IAsyncDisposable
{
  private readonly DockerCommandRunner runner;
  private readonly SemaphoreSlim buildLock = new(1, 1);
  private string? immutableImageId;
  private bool tagWasBuilt;
  private int disposed;

  public ContainerImageCoordinator(DockerCommandRunner runner)
  {
    this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
    OwnedTag = $"hevy-client:container-smoke-{Environment.ProcessId}-{RandomNumberGenerator.GetHexString(32).ToLowerInvariant()}";
  }

  public string OwnedTag { get; }

  public string ImmutableImageId => immutableImageId ??
      throw new InvalidOperationException("The container image has not been built and inspected.");

  public async Task<string> EnsureBuiltAsync(string repositoryRoot)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
    ObjectDisposedException.ThrowIf(disposed != 0, this);

    await buildLock.WaitAsync();
    try
    {
      if (immutableImageId is not null)
      {
        return immutableImageId;
      }

      var build = await runner(
          [
            "build",
            "--pull",
            "--build-arg", "VERSION=1.2.3-smoke.1",
            "--build-arg", "REVISION=0123456789abcdef0123456789abcdef01234567",
            "--build-arg", "SOURCE_URL=https://github.com/example/hevy-client",
            "--tag", OwnedTag,
            ".",
          ],
          repositoryRoot,
          TimeSpan.FromMinutes(10));
      if (build.ExitCode != 0)
      {
        throw new InvalidOperationException($"Container build failed.\nstdout:\n{build.StandardOutput}\nstderr:\n{build.StandardError}");
      }
      tagWasBuilt = true;

      var inspection = await runner(
          ["image", "inspect", "--format", "{{.Id}}", OwnedTag],
          repositoryRoot,
          TimeSpan.FromSeconds(30));
      var inspectedId = inspection.StandardOutput.Trim();
      if (inspection.ExitCode != 0 || !Regex.IsMatch(inspectedId, "^sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant))
      {
        throw new InvalidOperationException($"Built image identity inspection failed.\nstdout:\n{inspection.StandardOutput}\nstderr:\n{inspection.StandardError}");
      }

      immutableImageId = inspectedId;
      return inspectedId;
    }
    finally
    {
      buildLock.Release();
    }
  }

  public async ValueTask DisposeAsync()
  {
    if (Interlocked.Exchange(ref disposed, 1) != 0)
    {
      return;
    }

    if (tagWasBuilt)
    {
      await runner(
          ["image", "rm", OwnedTag],
          workingDirectory: null,
          timeout: TimeSpan.FromSeconds(30));
    }

    buildLock.Dispose();
  }
}

[CollectionDefinition("container-smoke", DisableParallelization = true)]
public sealed class ContainerSmokeCollection : ICollectionFixture<ContainerSmokeFixture>
{
}

public sealed class ContainerSmokeFixture : IAsyncLifetime
{
  private readonly ContainerImageCoordinator coordinator = new(DockerProcess.RunAsync);

  public Task InitializeAsync() => Task.CompletedTask;

  public async Task<string> EnsureImageAsync()
  {
    var availability = await DockerProcess.RunAsync(
        ["info", "--format", "{{json .ServerVersion}}"],
        workingDirectory: null,
        timeout: TimeSpan.FromSeconds(30));
    var decision = await DockerAvailabilityPolicy.EvaluateAsync(
        new DockerProbeResult(
            availability.ExitCode,
            availability.StandardOutput,
            availability.StandardError,
            availability.ExecutableMissing),
        isCi: !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CI")),
        DockerProcess.RunAsync,
        configuredDockerHost: Environment.GetEnvironmentVariable("DOCKER_HOST"),
        configuredDockerContext: Environment.GetEnvironmentVariable("DOCKER_CONTEXT"));
    if (decision is DockerAvailabilityDecision.Skip)
    {
      throw SkipException.ForSkip($"Docker is genuinely unavailable: {availability.StandardError.Trim()}");
    }
    (decision is DockerAvailabilityDecision.Use).Should().BeTrue($"Docker is installed but its prerequisite check failed and cannot be skipped: {availability.StandardError.Trim()}");

    return await coordinator.EnsureBuiltAsync(DockerProcess.RepositoryRoot);
  }

  public Task DisposeAsync() => coordinator.DisposeAsync().AsTask();
}

public static class DockerProcess
{
  public static string RepositoryRoot { get; } = FindRepositoryRoot();

  public static Process Start(params string[] arguments)
  {
    var startInfo = new ProcessStartInfo
    {
      FileName = "docker",
      RedirectStandardInput = true,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
    };
    foreach (var argument in arguments)
    {
      startInfo.ArgumentList.Add(argument);
    }

    return Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start Docker.");
  }

  public static async Task<DockerCommandResult> RunAsync(
      IReadOnlyList<string> arguments,
      string? workingDirectory = null,
      TimeSpan? timeout = null)
  {
    var startInfo = new ProcessStartInfo
    {
      FileName = "docker",
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
      WorkingDirectory = workingDirectory ?? RepositoryRoot,
    };
    foreach (var argument in arguments)
    {
      startInfo.ArgumentList.Add(argument);
    }

    try
    {
      using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start Docker.");
      var standardOutput = process.StandardOutput.ReadToEndAsync();
      var standardError = process.StandardError.ReadToEndAsync();
      await process.WaitForExitAsync().WaitAsync(timeout ?? TimeSpan.FromSeconds(30));
      return new DockerCommandResult(process.ExitCode, await standardOutput, await standardError, ExecutableMissing: false);
    }
    catch (System.ComponentModel.Win32Exception exception)
    {
      return new DockerCommandResult(
          127,
          string.Empty,
          exception.Message,
          ExecutableMissing: exception.NativeErrorCode is 2 or 3);
    }
  }

  private static string FindRepositoryRoot()
  {
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
    {
      if (File.Exists(Path.Combine(directory.FullName, "HevyClient.slnx")))
      {
        return directory.FullName;
      }
    }

    throw new InvalidOperationException("Could not locate the hevy-client repository root.");
  }
}

[Collection("container-smoke")]
public sealed class ContainerSmokeTests
{
  private readonly ContainerSmokeFixture fixture;

  public ContainerSmokeTests(ContainerSmokeFixture fixture)
  {
    this.fixture = fixture;
  }

  [Fact]
  public void DockerfilePinsEveryRemoteFrontendAndBaseImageBySha256Digest()
  {
    var dockerfile = File.ReadAllLines(Path.Combine(DockerProcess.RepositoryRoot, "Dockerfile"));
    var references = dockerfile
        .Select(static line => line.Trim())
        .Where(static line => line.StartsWith("# syntax=", StringComparison.Ordinal) || line.StartsWith("FROM ", StringComparison.OrdinalIgnoreCase))
        .Select(static line => line.StartsWith("# syntax=", StringComparison.Ordinal)
            ? line["# syntax=".Length..]
            : line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1])
        .Where(static reference => !string.Equals(reference, "scratch", StringComparison.Ordinal))
        .ToArray();

    (references).Should().NotBeEmpty();
    (references).Should().AllSatisfy(reference => (reference).Should().MatchRegex("@sha256:[0-9a-f]{64}$"));
  }

  [Fact]
  public async Task ImageIsPinnedNonRootReadOnlyFriendlyAndDoesNotAdvertiseAPort()
  {
    var imageId = await fixture.EnsureImageAsync();

    var inspection = await DockerProcess.RunAsync(["image", "inspect", imageId]);
    (inspection.ExitCode).Should().Be(0);
    using var document = JsonDocument.Parse(inspection.StandardOutput);
    var config = document.RootElement[0].GetProperty("Config");

    (config.GetProperty("User").GetString()).Should().Be("app");
    (config.GetProperty("Entrypoint").EnumerateArray().Select(static value => value.GetString()!).ToArray()).Should().Equal(["dotnet", "Hevy.Mcp.dll"]);
    (config.TryGetProperty("ExposedPorts", out var exposedPorts) && exposedPorts.ValueKind is not JsonValueKind.Null).Should().BeFalse();

    var labels = config.GetProperty("Labels");
    (labels.GetProperty("org.opencontainers.image.licenses").GetString()).Should().Be("MIT");
    (labels.GetProperty("org.opencontainers.image.title").GetString()).Should().Be("hevy-client");
    (labels.GetProperty("org.opencontainers.image.source").GetString()).Should().Be("https://github.com/example/hevy-client");
    (labels.GetProperty("org.opencontainers.image.revision").GetString()).Should().Be("0123456789abcdef0123456789abcdef01234567");
    (labels.GetProperty("org.opencontainers.image.version").GetString()).Should().Be("1.2.3-smoke.1");
    foreach (var label in new[]
    {
      "org.opencontainers.image.description",
      "org.opencontainers.image.source",
      "org.opencontainers.image.revision",
      "org.opencontainers.image.version",
    })
    {
      (string.IsNullOrWhiteSpace(labels.GetProperty(label).GetString())).Should().BeFalse();
    }

    var shellAttempt = await DockerProcess.RunAsync(["run", "--rm", "--entrypoint", "/bin/sh", imageId, "-c", "exit 0"]);
    (shellAttempt.ExitCode).Should().NotBe(0);
    var packageManagerAttempt = await DockerProcess.RunAsync(["run", "--rm", "--entrypoint", "/usr/bin/apt-get", imageId, "--version"]);
    (packageManagerAttempt.ExitCode).Should().NotBe(0);
  }

  [Fact]
  public async Task StdioContainerCompletesARealMcpHandshakeWithStdinAttached()
  {
    var imageId = await fixture.EnsureImageAsync();
    using var process = DockerProcess.Start(
        "run", "--rm", "-i", "--read-only", "--tmpfs", "/tmp:rw,noexec,nosuid,size=16m",
        "-e", "HEVY_API_KEY=container-smoke-fixture-key", imageId);

    await process.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"container-smoke","version":"1.0"}}}""");
    await process.StandardInput.FlushAsync();
    using var initialize = await ReadProtocolMessageAsync(process);
    await process.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");
    await process.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}""");
    await process.StandardInput.FlushAsync();
    using var tools = await ReadProtocolMessageAsync(process);
    process.StandardInput.Close();
    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));

    (initialize.RootElement.GetProperty("result").GetProperty("serverInfo").GetProperty("name").GetString()).Should().Be("hevy-client");
    (tools.RootElement.GetProperty("result").GetProperty("tools").GetArrayLength()).Should().Be(28);
    (process.ExitCode).Should().Be(0);
    (await process.StandardOutput.ReadToEndAsync()).Should().Be(string.Empty);
    (await process.StandardError.ReadToEndAsync()).Should().Be(string.Empty);
  }

  [Theory]
  [InlineData("Darwin", "security")]
  [InlineData("Linux", "secret-tool")]
  public async Task PosixGuiWrapperRetrievesSecretAtRuntimeAndStartsARealMcpContainer(
      string platform,
      string providerExecutable)
  {
    if (OperatingSystem.IsWindows())
    {
      throw SkipException.ForSkip("The POSIX GUI wrapper smoke runs on macOS or Linux hosts.");
    }

    var imageId = await fixture.EnsureImageAsync();
    var wrapper = Path.Combine(DockerProcess.RepositoryRoot, "scripts", "hevy-client-mcp");
    (File.Exists(wrapper)).Should().BeTrue("The documented POSIX secret-backed MCP wrapper is required.");
    var fakeSecret = $"fixture-{RandomNumberGenerator.GetHexString(24)}";
    var temporaryDirectory = Path.Combine(Path.GetTempPath(), $"hevy-wrapper-{RandomNumberGenerator.GetHexString(16)}");
    Directory.CreateDirectory(temporaryDirectory);

    try
    {
      WriteExecutable(
          Path.Combine(temporaryDirectory, "uname"),
          $"#!/bin/sh\nprintf '%s\\n' '{platform}'\n");
      WriteExecutable(
          Path.Combine(temporaryDirectory, providerExecutable),
          "#!/bin/sh\nprintf '%s' \"$HEVY_WRAPPER_TEST_SECRET\"\n");

      var startInfo = new ProcessStartInfo
      {
        FileName = wrapper,
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
      };
      startInfo.Environment.Remove("HEVY_API_KEY");
      startInfo.Environment["HEVY_CLIENT_IMAGE"] = imageId;
      startInfo.Environment["HEVY_WRAPPER_TEST_SECRET"] = fakeSecret;
      startInfo.Environment["PATH"] = temporaryDirectory + Path.PathSeparator + startInfo.Environment["PATH"];

      using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start the POSIX MCP wrapper.");
      await process.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"secret-wrapper-smoke","version":"1.0"}}}""");
      await process.StandardInput.FlushAsync();
      using var initialize = await ReadProtocolMessageAsync(process);
      await process.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");
      await process.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}""");
      await process.StandardInput.FlushAsync();
      using var tools = await ReadProtocolMessageAsync(process);
      process.StandardInput.Close();
      await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));

      (initialize.RootElement.GetProperty("result").GetProperty("serverInfo").GetProperty("name").GetString()).Should().Be("hevy-client");
      (tools.RootElement.GetProperty("result").GetProperty("tools").GetArrayLength()).Should().Be(28);
      (process.ExitCode).Should().Be(0);
      (await process.StandardError.ReadToEndAsync()).Should().Be(string.Empty);
      (await File.ReadAllTextAsync(wrapper)).Should().NotContain(fakeSecret);
      (Directory.EnumerateFiles(temporaryDirectory)).Should().AllSatisfy(file => (File.ReadAllText(file)).Should().NotContain(fakeSecret));
    }
    finally
    {
      Directory.Delete(temporaryDirectory, recursive: true);
    }
  }

  [Fact]
  public void WindowsSecurePromptLauncherUsesProcessOnlyInheritanceAndRestoresTheEnvironment()
  {
    var launcher = Path.Combine(DockerProcess.RepositoryRoot, "scripts", "Start-HevyClient.ps1");
    (File.Exists(launcher)).Should().BeTrue("The documented Windows secure-prompt GUI launcher is required.");
    var script = File.ReadAllText(launcher);

    (script).Should().Contain("Read-Host -Prompt 'Hevy API key' -AsSecureString");
    (script).Should().Contain("[System.Management.Automation.PSCredential]::new");
    (script).Should().Contain("$credential.GetNetworkCredential().Password");
    (script).Should().Contain("[Environment]::SetEnvironmentVariable('HEVY_API_KEY', $plainKey, 'Process')");
    (script).Should().Contain("& $ClientPath");
    (script).Should().Contain("finally");
    (script).Should().Contain("[Environment]::SetEnvironmentVariable('HEVY_API_KEY', $previousKey, 'Process')");
    (script).Should().NotContain("'User'");
    (script).Should().NotContain("'Machine'");
    (script).Should().NotContainEquivalentOf("Set-Content");
    (script).Should().NotContainEquivalentOf("Out-File");
  }

  [Fact]
  public async Task HttpContainerPublishesOnlyToLoopbackAndProtectsMcpWhileHealthIsEmpty()
  {
    var imageId = await fixture.EnsureImageAsync();
    var started = await DockerProcess.RunAsync([
      "run", "--detach", "--rm", "--read-only", "--tmpfs", "/tmp:rw,noexec,nosuid,size=16m",
      "--publish", "127.0.0.1::8080",
      "-e", "HEVY_API_KEY=container-smoke-fixture-key",
      "-e", "HEVY_MCP_TRANSPORT=http",
      "-e", "MCP_AUTH_TOKEN=container-smoke-auth-token",
      "-e", "ASPNETCORE_URLS=http://0.0.0.0:8080",
      imageId,
    ]);
    (started.ExitCode).Should().Be(0);
    var containerId = started.StandardOutput.Trim();

    try
    {
      var port = await WaitForLoopbackPortAsync(containerId);
      var processOwner = await DockerProcess.RunAsync(["top", containerId, "-eo", "uid,pid"]);
      (processOwner.ExitCode).Should().Be(0);
      (processOwner.StandardOutput
          .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
          .Last()
          .Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]).Should().Be("1654");
      using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
      using var health = await WaitForHealthAsync(client);
      (health.StatusCode).Should().Be(HttpStatusCode.OK);
      (await health.Content.ReadAsStringAsync()).Should().Be(string.Empty);

      using var mcp = await client.PostAsync("/mcp", new StringContent("{}"));
      (mcp.StatusCode).Should().Be(HttpStatusCode.Unauthorized);
      (mcp.Headers.WwwAuthenticate.Single().Scheme).Should().Be("Bearer");
    }
    finally
    {
      await DockerProcess.RunAsync(["rm", "--force", containerId]);
    }
  }

  private static async Task<int> WaitForLoopbackPortAsync(string containerId)
  {
    for (var attempt = 0; attempt < 40; attempt++)
    {
      var portResult = await DockerProcess.RunAsync(["port", containerId, "8080/tcp"]);
      var binding = portResult.StandardOutput.Trim();
      if (portResult.ExitCode == 0 && binding.StartsWith("127.0.0.1:", StringComparison.Ordinal) &&
          int.TryParse(binding["127.0.0.1:".Length..], out var port))
      {
        return port;
      }

      await Task.Delay(TimeSpan.FromMilliseconds(250));
    }

    throw new TimeoutException("The HTTP container did not expose its loopback-only test binding.");
  }

  private static async Task<HttpResponseMessage> WaitForHealthAsync(HttpClient client)
  {
    Exception? lastException = null;
    for (var attempt = 0; attempt < 40; attempt++)
    {
      try
      {
        var response = await client.GetAsync("/healthz");
        if (response.StatusCode == HttpStatusCode.OK)
        {
          return response;
        }

        response.Dispose();
      }
      catch (HttpRequestException exception)
      {
        lastException = exception;
      }

      await Task.Delay(TimeSpan.FromMilliseconds(250));
    }

    throw new TimeoutException("The HTTP container did not become healthy.", lastException);
  }

  private static async Task<JsonDocument> ReadProtocolMessageAsync(Process process)
  {
    var line = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(20));
    if (string.IsNullOrWhiteSpace(line))
    {
      var error = process.HasExited ? await process.StandardError.ReadToEndAsync() : "process is still running";
      false.Should().BeTrue($"The MCP process produced no protocol response; {error}");
    }
    return JsonDocument.Parse(line!);
  }

  private static void WriteExecutable(string path, string contents)
  {
    if (OperatingSystem.IsWindows())
    {
      throw new PlatformNotSupportedException("POSIX executable fixtures are unavailable on Windows.");
    }

    File.WriteAllText(path, contents);
    File.SetUnixFileMode(
        path,
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
  }
}

public sealed class ContainerSmokeInfrastructureTests
{
  private const string LocalDockerEndpoint = "unix:///var/run/docker.sock";

  private static Task<DockerAvailabilityDecision> EvaluateWithLocalEndpointAsync(
      DockerProbeResult probe,
      bool isCi) => DockerAvailabilityPolicy.EvaluateAsync(
          probe,
          isCi,
          UnexpectedDocker,
          configuredDockerHost: LocalDockerEndpoint,
          configuredDockerContext: null);

  private static Task<DockerCommandResult> UnexpectedDocker(
      IReadOnlyList<string> arguments,
      string? workingDirectory,
      TimeSpan? timeout) => throw new InvalidOperationException(
          $"Configured DOCKER_HOST should avoid context inspection, but ran: {string.Join(' ', arguments)}");

  [Theory]
  [InlineData(false, DockerAvailabilityDecision.Skip)]
  [InlineData(true, DockerAvailabilityDecision.Fail)]
  public async Task MissingDockerExecutableSkipsOnlyOutsideCi(bool isCi, DockerAvailabilityDecision expected)
  {
    var probe = new DockerProbeResult(127, string.Empty, "docker executable was not found", ExecutableMissing: true);

    (await DockerAvailabilityPolicy.EvaluateAsync(
            probe,
            isCi,
            UnexpectedDocker,
            configuredDockerHost: "ssh://builder@build-host",
            configuredDockerContext: "production")).Should().Be(expected);
  }

  [Theory]
  [InlineData("Cannot connect to the Docker daemon at unix:///var/run/docker.sock. Is the docker daemon running?")]
  [InlineData("Cannot connect to the Docker daemon at unix:///home/user/.docker/desktop/docker.sock. Is the docker daemon running?")]
  [InlineData("error during connect: Get http://%2F%2F.%2Fpipe%2Fdocker_engine/v1.24/info: open //./pipe/docker_engine: The system cannot find the file specified")]
  [InlineData("error during connect: in the default daemon configuration on Windows, the docker client must be run with elevated privileges to connect: Get \"http://%2F%2F.%2Fpipe%2Fdocker_engine/v1.24/info\": open //./pipe/docker_engine: The system cannot find the file specified.")]
  [InlineData("error during connect: Get \"http://%2F%2F.%2Fpipe%2FdockerDesktopLinuxEngine/v1.47/info\": open //./pipe/dockerDesktopLinuxEngine: The system cannot find the file specified.")]
  [InlineData("failed to connect to the docker API at tcp://localhost:2375: dial tcp [::1]:2375: connect: connection refused")]
  [InlineData("failed to connect to the docker API at tcp://127.0.0.1:2375: dial tcp 127.0.0.1:2375: connect: connection refused")]
  [InlineData("failed to connect to the docker API at tcp://[::1]:2375: dial tcp [::1]:2375: connect: connection refused")]
  [InlineData("failed to connect to the docker API at tcp://localhost:2375; check if the path is correct and if the daemon is running: dial tcp [::1]:2375: connect: connection refused")]
  [InlineData("failed to connect to the docker API at tcp://127.0.0.1:2375: connection refused")]
  public async Task RecognizedStoppedLocalDaemonSkipsOnlyOutsideCi(string error)
  {
    var probe = new DockerProbeResult(
        1,
        string.Empty,
        error,
        ExecutableMissing: false);

    (await EvaluateWithLocalEndpointAsync(probe, isCi: false)).Should().Be(DockerAvailabilityDecision.Skip);
    (await EvaluateWithLocalEndpointAsync(probe, isCi: true)).Should().Be(DockerAvailabilityDecision.Fail);
  }

  [Theory]
  [InlineData("failed to connect to the docker API at tcp://build-host:2375: dial tcp 10.40.0.12:2375: connect: connection refused")]
  [InlineData("failed to connect to the docker API at tcp://192.0.2.40:2375: dial tcp 192.0.2.40:2375: connect: connection refused")]
  [InlineData("failed to connect to the docker API at tcp://[2001:db8::40]:2375: dial tcp [2001:db8::40]:2375: connect: connection refused")]
  [InlineData("Cannot connect to the Docker daemon at ssh://build-host. Is the docker daemon running?")]
  [InlineData("failed to connect to the docker API at ssh://builder@build-host:22: connection refused")]
  public async Task RemoteDockerEndpointsNeverSkip(string error)
  {
    var probe = new DockerProbeResult(1, string.Empty, error, ExecutableMissing: false);

    (await EvaluateWithLocalEndpointAsync(probe, isCi: false)).Should().Be(DockerAvailabilityDecision.Fail);
    (await EvaluateWithLocalEndpointAsync(probe, isCi: true)).Should().Be(DockerAvailabilityDecision.Fail);
  }

  [Theory]
  [InlineData("error during connect: Get https://build-host.invalid/--//./pipe/dockerized: The system cannot find the file specified")]
  [InlineData("error during connect: nonsense //./pipe/docker-not-a-pipe The system cannot find the file specified")]
  [InlineData("failed to connect to the docker API at tcp://localhost::: connection refused")]
  [InlineData("failed to connect to the docker API at tcp://localhost:2375: unrelated; failed to connect to the docker API at tcp://build-host:2375: connection refused")]
  [InlineData("failed to connect to the docker API at tcp://user@localhost:2375: connection refused")]
  [InlineData("failed to connect to the docker API at tcp://localhost.invalid:2375: connection refused")]
  [InlineData("failed to connect to the docker API at tcp://localhost:70000: connection refused")]
  [InlineData("failed to connect to the docker API at tcp://localhost:2375.: connection refused")]
  [InlineData("error during connect: Get http://%2F%2F.%2Fpipe%2Fdocker_engine-evil/v1.24/info: open //./pipe/docker_engine-evil: The system cannot find the file specified")]
  [InlineData("error during connect: Get \"http://%2F%2F.%2Fpipe%2Fdocker_engine/v1.24/info: open //./pipe/docker_engine: The system cannot find the file specified")]
  [InlineData("error during connect: Get http://%2F%2F.%2Fpipe%2Fdocker_engine/v1.24/info\": open //./pipe/docker_engine: The system cannot find the file specified")]
  [InlineData("failed to connect to the docker API at tcp://localhost:2375/path/..: connection refused")]
  [InlineData("failed to connect to the docker API at tcp://localhost:2375/.: connection refused")]
  [InlineData("failed to connect to the docker API at tcp://localhost:2375/%2e: connection refused")]
  [InlineData("failed to connect to the docker API at tcp://[::1]:2375/path/..: connection refused")]
  [InlineData("Cannot connect to the Docker daemon at unix:////. Is the docker daemon running?")]
  public async Task AdversarialDaemonDiagnosticsFailClosed(string error)
  {
    var probe = new DockerProbeResult(1, string.Empty, error, ExecutableMissing: false);

    (await EvaluateWithLocalEndpointAsync(probe, isCi: false)).Should().Be(DockerAvailabilityDecision.Fail);
    (await EvaluateWithLocalEndpointAsync(probe, isCi: true)).Should().Be(DockerAvailabilityDecision.Fail);
  }

  [Theory]
  [InlineData("\r")]
  [InlineData("\n")]
  [InlineData("\r\n")]
  public async Task EvenTrailingDiagnosticLineControlsFailClosed(string separator)
  {
    var error =
        "Cannot connect to the Docker daemon at unix:///var/run/docker.sock. Is the docker daemon running?" +
        separator;
    var probe = new DockerProbeResult(1, string.Empty, error, ExecutableMissing: false);

    (await EvaluateWithLocalEndpointAsync(probe, isCi: false)).Should().Be(DockerAvailabilityDecision.Fail);
  }

  [Theory]
  [InlineData("\0")]
  [InlineData("\u0001")]
  [InlineData("\u000b")]
  [InlineData("\u001b")]
  [InlineData("\u0085")]
  [InlineData("\u2028")]
  [InlineData("\u2029")]
  public async Task UnicodeLineAndControlCharactersAnywhereInDiagnosticsFailClosed(string separator)
  {
    const string diagnostic =
        "error during connect: Get http://%2F%2F.%2Fpipe%2Fdocker_engine/v1.24/info: open //./pipe/docker_engine: The system cannot find the file specified";
    var positions = new[]
    {
      0,
      diagnostic.IndexOf("/info", StringComparison.Ordinal),
      diagnostic.Length,
    };

    foreach (var position in positions)
    {
      var probe = new DockerProbeResult(
          1,
          string.Empty,
          diagnostic.Insert(position, separator),
          ExecutableMissing: false);
      (await EvaluateWithLocalEndpointAsync(probe, isCi: false)).Should().Be(DockerAvailabilityDecision.Fail);
    }
  }

  [Fact]
  public async Task PersistedActiveRemoteContextPreventsLocalAbsenceSkip()
  {
    var probe = new DockerProbeResult(
        1,
        string.Empty,
        "Cannot connect to the Docker daemon at unix:///var/run/docker.sock. Is the docker daemon running?",
        ExecutableMissing: false);

    Task<DockerCommandResult> FakeDocker(
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        TimeSpan? timeout) => arguments switch
        {
          ["context", "show"] => Task.FromResult(
              new DockerCommandResult(0, "production\n", string.Empty, ExecutableMissing: false)),
          ["context", "inspect", "--format", _, "--", "production"] => Task.FromResult(
              new DockerCommandResult(0, "tcp://build-host:2375\n", string.Empty, ExecutableMissing: false)),
          _ => throw new InvalidOperationException($"Unexpected fake Docker command: {string.Join(' ', arguments)}"),
        };

    (await DockerAvailabilityPolicy.EvaluateAsync(
            probe,
            isCi: false,
            FakeDocker,
            configuredDockerHost: null,
            configuredDockerContext: null)).Should().Be(DockerAvailabilityDecision.Fail);
  }

  [Theory]
  [InlineData("default", "tcp://build-host:2375", DockerAvailabilityDecision.Fail)]
  [InlineData("desktop-linux", "ssh://builder@build-host", DockerAvailabilityDecision.Fail)]
  [InlineData("default", "unix:///var/run/docker.sock", DockerAvailabilityDecision.Skip)]
  [InlineData("desktop-linux", "unix:///home/user/.docker/desktop/docker.sock", DockerAvailabilityDecision.Skip)]
  public async Task NamedContextIsClassifiedByItsInspectedEndpoint(
      string contextName,
      string endpoint,
      DockerAvailabilityDecision expected)
  {
    var probe = new DockerProbeResult(
        1,
        string.Empty,
        "Cannot connect to the Docker daemon at unix:///var/run/docker.sock. Is the docker daemon running?",
        ExecutableMissing: false);

    Task<DockerCommandResult> FakeDocker(
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        TimeSpan? timeout) => arguments switch
        {
          ["context", "inspect", "--format", _, "--", var inspectedContext]
              when inspectedContext == contextName => Task.FromResult(
                  new DockerCommandResult(0, $"{endpoint}\n", string.Empty, ExecutableMissing: false)),
          _ => throw new InvalidOperationException($"Unexpected fake Docker command: {string.Join(' ', arguments)}"),
        };

    (await DockerAvailabilityPolicy.EvaluateAsync(
            probe,
            isCi: false,
            FakeDocker,
            configuredDockerHost: null,
            configuredDockerContext: contextName)).Should().Be(expected);
  }

  [Fact]
  public async Task FailedContextInspectionFailsClosed()
  {
    var probe = new DockerProbeResult(
        1,
        string.Empty,
        "Cannot connect to the Docker daemon at unix:///var/run/docker.sock. Is the docker daemon running?",
        ExecutableMissing: false);

    Task<DockerCommandResult> FakeDocker(
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        TimeSpan? timeout) => arguments switch
        {
          ["context", "show"] => Task.FromResult(
              new DockerCommandResult(0, "default\n", string.Empty, ExecutableMissing: false)),
          ["context", "inspect", "--format", _, "--", "default"] => Task.FromResult(
              new DockerCommandResult(1, string.Empty, "context inspection failed", ExecutableMissing: false)),
          _ => throw new InvalidOperationException($"Unexpected fake Docker command: {string.Join(' ', arguments)}"),
        };

    (await DockerAvailabilityPolicy.EvaluateAsync(
            probe,
            isCi: false,
            FakeDocker,
            configuredDockerHost: null,
            configuredDockerContext: null)).Should().Be(DockerAvailabilityDecision.Fail);
  }

  [Fact]
  public async Task ContextResolutionRunnerFailureFailsClosed()
  {
    var probe = new DockerProbeResult(
        1,
        string.Empty,
        "Cannot connect to the Docker daemon at unix:///var/run/docker.sock. Is the docker daemon running?",
        ExecutableMissing: false);

    Task<DockerCommandResult> FailingDocker(
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        TimeSpan? timeout) => throw new TimeoutException("context command timed out");

    (await DockerAvailabilityPolicy.EvaluateAsync(
            probe,
            isCi: false,
            FailingDocker,
            configuredDockerHost: null,
            configuredDockerContext: null)).Should().Be(DockerAvailabilityDecision.Fail);
  }

  [Theory]
  [InlineData("\0")]
  [InlineData("\r")]
  [InlineData("\n")]
  [InlineData("\u0085")]
  [InlineData("\u2028")]
  [InlineData("\u2029")]
  public async Task UnsafeConfiguredContextNameFailsBeforeInspection(string separator)
  {
    var probe = new DockerProbeResult(
        1,
        string.Empty,
        "Cannot connect to the Docker daemon at unix:///var/run/docker.sock. Is the docker daemon running?",
        ExecutableMissing: false);

    (await DockerAvailabilityPolicy.EvaluateAsync(
            probe,
            isCi: false,
            UnexpectedDocker,
            configuredDockerHost: null,
            configuredDockerContext: $"default{separator}")).Should().Be(DockerAvailabilityDecision.Fail);
  }

  [Theory]
  [InlineData("local", "tcp://build-host:2375", DockerAvailabilityDecision.Fail)]
  [InlineData("production", "unix:///var/run/docker.sock", DockerAvailabilityDecision.Skip)]
  public async Task NonemptyDockerHostOverridesDockerContext(
      string configuredDockerContext,
      string configuredDockerHost,
      DockerAvailabilityDecision expected)
  {
    var probe = new DockerProbeResult(
        1,
        string.Empty,
        "Cannot connect to the Docker daemon at unix:///var/run/docker.sock. Is the docker daemon running?",
        ExecutableMissing: false);

    (await DockerAvailabilityPolicy.EvaluateAsync(
            probe,
            isCi: false,
            UnexpectedDocker,
            configuredDockerHost,
            configuredDockerContext)).Should().Be(expected);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  public async Task BlankDockerHostFallsThroughToConfiguredContext(string? configuredDockerHost)
  {
    const string configuredDockerContext = "local";
    var probe = new DockerProbeResult(
        1,
        string.Empty,
        "Cannot connect to the Docker daemon at unix:///var/run/docker.sock. Is the docker daemon running?",
        ExecutableMissing: false);

    Task<DockerCommandResult> FakeDocker(
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        TimeSpan? timeout)
    {
      (arguments).Should().Equal(["context", "inspect", "--format", "{{(index .Endpoints \"docker\").Host}}", "--", configuredDockerContext]);
      return Task.FromResult(
          new DockerCommandResult(0, "unix:///var/run/docker.sock\n", string.Empty, ExecutableMissing: false));
    }

    (await DockerAvailabilityPolicy.EvaluateAsync(
            probe,
            isCi: false,
            FakeDocker,
            configuredDockerHost,
            configuredDockerContext)).Should().Be(DockerAvailabilityDecision.Skip);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  public async Task DockerHostIsUsedWhenDockerContextIsAbsent(string? configuredDockerContext)
  {
    var probe = new DockerProbeResult(
        1,
        string.Empty,
        "Cannot connect to the Docker daemon at unix:///var/run/docker.sock. Is the docker daemon running?",
        ExecutableMissing: false);

    (await DockerAvailabilityPolicy.EvaluateAsync(
            probe,
            isCi: false,
            UnexpectedDocker,
            configuredDockerHost: LocalDockerEndpoint,
            configuredDockerContext)).Should().Be(DockerAvailabilityDecision.Skip);
  }

  public static TheoryData<string> InvalidDockerContextNames => new()
  {
    "--format=unix:///var/run/docker.sock",
    "-f=unix:///var/run/docker.sock",
    " ",
    "default remote",
    "default;remote",
    "default|remote",
    "../default",
    "default/../remote",
    "C:\\docker",
    "a",
    "caf\u00e9",
    new string('a', 257),
  };

  public static TheoryData<string> ValidDockerContextNames => new()
  {
    "default",
    "desktop-linux",
    "prod_eu.v2+blue",
    new string('a', 256),
  };

  [Theory]
  [MemberData(nameof(InvalidDockerContextNames))]
  public async Task ConfiguredContextNamesOutsideDockerGrammarFailBeforeInspection(string configuredDockerContext)
  {
    var probe = new DockerProbeResult(
        1,
        string.Empty,
        "Cannot connect to the Docker daemon at unix:///var/run/docker.sock. Is the docker daemon running?",
        ExecutableMissing: false);

    (await DockerAvailabilityPolicy.EvaluateAsync(
            probe,
            isCi: false,
            UnexpectedDocker,
            configuredDockerHost: null,
            configuredDockerContext)).Should().Be(DockerAvailabilityDecision.Fail);
  }

  [Theory]
  [MemberData(nameof(InvalidDockerContextNames))]
  public async Task PersistedContextNamesOutsideDockerGrammarFailBeforeInspection(string activeContext)
  {
    var probe = new DockerProbeResult(
        1,
        string.Empty,
        "Cannot connect to the Docker daemon at unix:///var/run/docker.sock. Is the docker daemon running?",
        ExecutableMissing: false);

    var commandCount = 0;
    Task<DockerCommandResult> FakeDocker(
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        TimeSpan? timeout)
    {
      commandCount++;
      (arguments).Should().Equal(["context", "show"]);
      return Task.FromResult(
          new DockerCommandResult(0, $"{activeContext}\n", string.Empty, ExecutableMissing: false));
    }

    (await DockerAvailabilityPolicy.EvaluateAsync(
            probe,
            isCi: false,
            FakeDocker,
            configuredDockerHost: null,
            configuredDockerContext: null)).Should().Be(DockerAvailabilityDecision.Fail);
    (commandCount).Should().Be(1);
  }

  [Theory]
  [MemberData(nameof(ValidDockerContextNames))]
  public async Task ValidDockerContextNamesAreInspectedAsProtectedPositionals(string configuredDockerContext)
  {
    var probe = new DockerProbeResult(
        1,
        string.Empty,
        "Cannot connect to the Docker daemon at unix:///var/run/docker.sock. Is the docker daemon running?",
        ExecutableMissing: false);

    Task<DockerCommandResult> FakeDocker(
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        TimeSpan? timeout)
    {
      (arguments).Should().Equal(["context", "inspect", "--format", "{{(index .Endpoints \"docker\").Host}}", "--", configuredDockerContext]);
      return Task.FromResult(
          new DockerCommandResult(0, "unix:///var/run/docker.sock\n", string.Empty, ExecutableMissing: false));
    }

    (await DockerAvailabilityPolicy.EvaluateAsync(
            probe,
            isCi: false,
            FakeDocker,
            configuredDockerHost: null,
            configuredDockerContext)).Should().Be(DockerAvailabilityDecision.Skip);
  }

  [Theory]
  [InlineData("tcp://localhost:2375/path/..")]
  [InlineData("tcp://localhost:2375/.")]
  [InlineData("tcp://localhost:2375/%2e")]
  [InlineData("tcp://[::1]:2375/path/..")]
  [InlineData("tcp://localhost:2375/")]
  [InlineData("tcp://user@localhost:2375")]
  [InlineData("tcp://localhost:2375?mode=local")]
  [InlineData("tcp://localhost:2375#local")]
  [InlineData("unix:////")]
  [InlineData("unix:///var/run/../docker.sock")]
  [InlineData("unix:///var/run/%64ocker.sock")]
  [InlineData("npipe:////./pipe/docker_engine/extra")]
  public async Task EffectiveEndpointRejectsNonCanonicalRawGrammar(string configuredDockerHost)
  {
    var probe = new DockerProbeResult(
        1,
        string.Empty,
        "Cannot connect to the Docker daemon at unix:///var/run/docker.sock. Is the docker daemon running?",
        ExecutableMissing: false);

    Task<DockerCommandResult> UnexpectedDocker(
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        TimeSpan? timeout) => throw new InvalidOperationException(
            $"DOCKER_HOST should avoid context inspection, but ran: {string.Join(' ', arguments)}");

    (await DockerAvailabilityPolicy.EvaluateAsync(
            probe,
            isCi: false,
            UnexpectedDocker,
            configuredDockerHost,
            configuredDockerContext: null)).Should().Be(DockerAvailabilityDecision.Fail);
  }

  [Theory]
  [InlineData("\0")]
  [InlineData("\u0001")]
  [InlineData("\r")]
  [InlineData("\n")]
  [InlineData("\u0085")]
  [InlineData("\u2028")]
  [InlineData("\u2029")]
  public async Task UnicodeLineAndControlCharactersAnywhereInEndpointsFailClosed(string separator)
  {
    const string endpoint = "tcp://localhost:2375";
    var probe = new DockerProbeResult(
        1,
        string.Empty,
        "Cannot connect to the Docker daemon at unix:///var/run/docker.sock. Is the docker daemon running?",
        ExecutableMissing: false);

    Task<DockerCommandResult> UnexpectedDocker(
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        TimeSpan? timeout) => throw new InvalidOperationException(
            $"DOCKER_HOST should avoid context inspection, but ran: {string.Join(' ', arguments)}");

    foreach (var position in new[] { 0, endpoint.Length / 2, endpoint.Length })
    {
      (await DockerAvailabilityPolicy.EvaluateAsync(
              probe,
              isCi: false,
              UnexpectedDocker,
              endpoint.Insert(position, separator),
              configuredDockerContext: null)).Should().Be(DockerAvailabilityDecision.Fail);
    }
  }

  [Theory]
  [InlineData("tcp://localhost:2375")]
  [InlineData("tcp://127.0.0.1:2375")]
  [InlineData("tcp://[::1]:2375")]
  [InlineData("http://localhost:2375")]
  [InlineData("https://127.0.0.1:2376")]
  [InlineData("unix:///var/run/docker.sock")]
  [InlineData("unix:///home/user/.docker/desktop/docker.sock")]
  [InlineData("npipe:////./pipe/docker_engine")]
  [InlineData("npipe:////./pipe/dockerDesktopLinuxEngine")]
  public async Task EffectiveEndpointAcceptsExactLocalGrammar(string configuredDockerHost)
  {
    var probe = new DockerProbeResult(
        1,
        string.Empty,
        "Cannot connect to the Docker daemon at unix:///var/run/docker.sock. Is the docker daemon running?",
        ExecutableMissing: false);

    Task<DockerCommandResult> UnexpectedDocker(
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        TimeSpan? timeout) => throw new InvalidOperationException(
            $"DOCKER_HOST should avoid context inspection, but ran: {string.Join(' ', arguments)}");

    (await DockerAvailabilityPolicy.EvaluateAsync(
            probe,
            isCi: false,
            UnexpectedDocker,
            configuredDockerHost,
            configuredDockerContext: null)).Should().Be(DockerAvailabilityDecision.Skip);
  }

  [Theory]
  [InlineData("permission denied while trying to connect to the Docker daemon socket")]
  [InlineData("remote error: tls: bad certificate")]
  [InlineData("context named production does not exist")]
  [InlineData("client API version is too old")]
  [InlineData("arbitrary exit one")]
  public async Task ArbitraryDockerFailuresNeverSkip(string error)
  {
    var probe = new DockerProbeResult(1, string.Empty, error, ExecutableMissing: false);

    (await EvaluateWithLocalEndpointAsync(probe, isCi: false)).Should().Be(DockerAvailabilityDecision.Fail);
    (await EvaluateWithLocalEndpointAsync(probe, isCi: true)).Should().Be(DockerAvailabilityDecision.Fail);
  }

  [Fact]
  public async Task IndependentImageCoordinatorsUseUniqueTagsAndBindToTheirOwnImmutableIds()
  {
    var builtTags = new System.Collections.Concurrent.ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
    var removedTags = new System.Collections.Concurrent.ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
    async Task<DockerCommandResult> FakeDocker(
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        TimeSpan? timeout)
    {
      await Task.Yield();
      if (arguments[0] == "build")
      {
        var tag = arguments[Array.IndexOf(arguments.ToArray(), "--tag") + 1];
        (builtTags.TryAdd(tag, 0)).Should().BeTrue($"Build tag collided: {tag}");
        return new DockerCommandResult(0, string.Empty, string.Empty, ExecutableMissing: false);
      }

      if (arguments is ["image", "inspect", "--format", _, var inspectedTag])
      {
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(inspectedTag))).ToLowerInvariant();
        return new DockerCommandResult(0, $"sha256:{digest}\n", string.Empty, ExecutableMissing: false);
      }

      if (arguments is ["image", "rm", var removedTag])
      {
        (removedTags.TryAdd(removedTag, 0)).Should().BeTrue($"Tag was cleaned more than once: {removedTag}");
        return new DockerCommandResult(0, string.Empty, string.Empty, ExecutableMissing: false);
      }

      throw new InvalidOperationException($"Unexpected fake Docker command: {string.Join(' ', arguments)}");
    }

    await using var first = new ContainerImageCoordinator(FakeDocker);
    await using var second = new ContainerImageCoordinator(FakeDocker);

    var ids = await Task.WhenAll(
        first.EnsureBuiltAsync(DockerProcess.RepositoryRoot),
        second.EnsureBuiltAsync(DockerProcess.RepositoryRoot));

    (second.OwnedTag).Should().NotBe(first.OwnedTag);
    (first.OwnedTag).Should().MatchRegex("^hevy-client:container-smoke-[0-9]+-[0-9a-f]{32}$");
    (first.ImmutableImageId).Should().MatchRegex("^sha256:[0-9a-f]{64}$");
    (first.ImmutableImageId).Should().Be(ids[0]);
    (second.ImmutableImageId).Should().Be(ids[1]);
    (ids[1]).Should().NotBe(ids[0]);

    await first.DisposeAsync();
    await second.DisposeAsync();
    (removedTags.Keys.Order(StringComparer.Ordinal).ToArray()).Should().Equal(new[] { first.OwnedTag, second.OwnedTag }.Order(StringComparer.Ordinal).ToArray());
  }

  [Fact]
  public async Task CoordinatorCleansItsExactTagWhenImmutableIdentityInspectionFails()
  {
    string? removedTag = null;
    Task<DockerCommandResult> FakeDocker(
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        TimeSpan? timeout)
    {
      if (arguments[0] == "build")
      {
        return Task.FromResult(new DockerCommandResult(0, string.Empty, string.Empty, ExecutableMissing: false));
      }

      if (arguments[0] == "image" && arguments[1] == "inspect")
      {
        return Task.FromResult(new DockerCommandResult(1, string.Empty, "inspection failed", ExecutableMissing: false));
      }

      if (arguments is ["image", "rm", var tag])
      {
        removedTag = tag;
        return Task.FromResult(new DockerCommandResult(0, string.Empty, string.Empty, ExecutableMissing: false));
      }

      throw new InvalidOperationException($"Unexpected fake Docker command: {string.Join(' ', arguments)}");
    }

    var coordinator = new ContainerImageCoordinator(FakeDocker);
    await FluentActions.Awaiting(() => coordinator.EnsureBuiltAsync(DockerProcess.RepositoryRoot)).Should().ThrowExactlyAsync<InvalidOperationException>();

    await coordinator.DisposeAsync();

    (removedTag).Should().Be(coordinator.OwnedTag);
  }

  [Fact]
  public void PublicDistributionIsBlockedUntilPrivateSecurityReportingIsEnabled()
  {
    var security = File.ReadAllText(Path.Combine(DockerProcess.RepositoryRoot, "SECURITY.md"));
    var checklistPath = Path.Combine(DockerProcess.RepositoryRoot, "docs", "release-checklist.md");

    (security).Should().Contain("](../../security/advisories/new)");
    (security).Should().ContainEquivalentOf("must be enabled before public distribution");
    (security).Should().Contain("Do not open a public issue for a suspected vulnerability");
    (File.Exists(checklistPath)).Should().BeTrue("The public-distribution release checklist is required.");

    var checklist = File.ReadAllText(checklistPath);
    (checklist).Should().Contain("BLOCK RELEASE");
    (checklist).Should().ContainEquivalentOf("private vulnerability reporting");
    (checklist).Should().Contain("../../security/advisories/new");
  }

  [Fact]
  public void DesktopClientDocumentationUsesOperationalSecretBackedLaunchers()
  {
    var readme = File.ReadAllText(Path.Combine(DockerProcess.RepositoryRoot, "README.md"));

    (readme).Should().Contain("scripts/hevy-client-mcp");
    (readme).Should().Contain("scripts/Start-HevyClient.ps1");
    (readme).Should().Contain("macOS Keychain");
    (readme).Should().Contain("Linux Secret Service");
    (readme).Should().Contain("Windows");
    (readme).Should().Contain("\"command\": \"/absolute/path/to/hevy-client-mcp\"");
    (readme).Should().NotContainEquivalentOf("restart a desktop client after setting it");
  }
}
