using System.Diagnostics;
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
  public static DockerAvailabilityDecision Evaluate(DockerProbeResult probe, bool isCi)
  {
    ArgumentNullException.ThrowIfNull(probe);

    if (probe.ExitCode == 0)
    {
      return DockerAvailabilityDecision.Use;
    }

    if (isCi)
    {
      return DockerAvailabilityDecision.Fail;
    }

    return probe.ExecutableMissing || IsRecognizedStoppedLocalDaemon(probe.StandardError)
        ? DockerAvailabilityDecision.Skip
        : DockerAvailabilityDecision.Fail;
  }

  private static bool IsRecognizedStoppedLocalDaemon(string error) =>
      (error.Contains("Cannot connect to the Docker daemon at ", StringComparison.Ordinal) &&
       error.Contains("Is the docker daemon running?", StringComparison.Ordinal)) ||
      (error.Contains("error during connect:", StringComparison.OrdinalIgnoreCase) &&
       error.Contains("//./pipe/docker", StringComparison.OrdinalIgnoreCase) &&
       error.Contains("The system cannot find the file specified", StringComparison.Ordinal)) ||
      (error.Contains("failed to connect to the docker API at ", StringComparison.OrdinalIgnoreCase) &&
       error.Contains("connection refused", StringComparison.OrdinalIgnoreCase));
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
    var decision = DockerAvailabilityPolicy.Evaluate(
        new DockerProbeResult(
            availability.ExitCode,
            availability.StandardOutput,
            availability.StandardError,
            availability.ExecutableMissing),
        isCi: !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CI")));
    if (decision is DockerAvailabilityDecision.Skip)
    {
      throw SkipException.ForSkip($"Docker is genuinely unavailable: {availability.StandardError.Trim()}");
    }
    Assert.True(
        decision is DockerAvailabilityDecision.Use,
        $"Docker is installed but its prerequisite check failed and cannot be skipped: {availability.StandardError.Trim()}");

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

    Assert.NotEmpty(references);
    Assert.All(references, reference => Assert.Matches("@sha256:[0-9a-f]{64}$", reference));
  }

  [Fact]
  public async Task ImageIsPinnedNonRootReadOnlyFriendlyAndDoesNotAdvertiseAPort()
  {
    var imageId = await fixture.EnsureImageAsync();

    var inspection = await DockerProcess.RunAsync(["image", "inspect", imageId]);
    Assert.Equal(0, inspection.ExitCode);
    using var document = JsonDocument.Parse(inspection.StandardOutput);
    var config = document.RootElement[0].GetProperty("Config");

    Assert.Equal("app", config.GetProperty("User").GetString());
    Assert.Equal(["dotnet", "Hevy.Mcp.dll"], config.GetProperty("Entrypoint").EnumerateArray().Select(static value => value.GetString()!).ToArray());
    Assert.False(config.TryGetProperty("ExposedPorts", out var exposedPorts) && exposedPorts.ValueKind is not JsonValueKind.Null);

    var labels = config.GetProperty("Labels");
    Assert.Equal("MIT", labels.GetProperty("org.opencontainers.image.licenses").GetString());
    Assert.Equal("hevy-client", labels.GetProperty("org.opencontainers.image.title").GetString());
    Assert.Equal("https://github.com/example/hevy-client", labels.GetProperty("org.opencontainers.image.source").GetString());
    Assert.Equal("0123456789abcdef0123456789abcdef01234567", labels.GetProperty("org.opencontainers.image.revision").GetString());
    Assert.Equal("1.2.3-smoke.1", labels.GetProperty("org.opencontainers.image.version").GetString());
    foreach (var label in new[]
    {
      "org.opencontainers.image.description",
      "org.opencontainers.image.source",
      "org.opencontainers.image.revision",
      "org.opencontainers.image.version",
    })
    {
      Assert.False(string.IsNullOrWhiteSpace(labels.GetProperty(label).GetString()));
    }

    var shellAttempt = await DockerProcess.RunAsync(["run", "--rm", "--entrypoint", "/bin/sh", imageId, "-c", "exit 0"]);
    Assert.NotEqual(0, shellAttempt.ExitCode);
    var packageManagerAttempt = await DockerProcess.RunAsync(["run", "--rm", "--entrypoint", "/usr/bin/apt-get", imageId, "--version"]);
    Assert.NotEqual(0, packageManagerAttempt.ExitCode);
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

    Assert.Equal("hevy-client", initialize.RootElement.GetProperty("result").GetProperty("serverInfo").GetProperty("name").GetString());
    Assert.Equal(28, tools.RootElement.GetProperty("result").GetProperty("tools").GetArrayLength());
    Assert.Equal(0, process.ExitCode);
    Assert.Equal(string.Empty, await process.StandardOutput.ReadToEndAsync());
    Assert.Equal(string.Empty, await process.StandardError.ReadToEndAsync());
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
    Assert.True(File.Exists(wrapper), "The documented POSIX secret-backed MCP wrapper is required.");
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

      Assert.Equal("hevy-client", initialize.RootElement.GetProperty("result").GetProperty("serverInfo").GetProperty("name").GetString());
      Assert.Equal(28, tools.RootElement.GetProperty("result").GetProperty("tools").GetArrayLength());
      Assert.Equal(0, process.ExitCode);
      Assert.Equal(string.Empty, await process.StandardError.ReadToEndAsync());
      Assert.DoesNotContain(fakeSecret, await File.ReadAllTextAsync(wrapper), StringComparison.Ordinal);
      Assert.All(
          Directory.EnumerateFiles(temporaryDirectory),
          file => Assert.DoesNotContain(fakeSecret, File.ReadAllText(file), StringComparison.Ordinal));
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
    Assert.True(File.Exists(launcher), "The documented Windows secure-prompt GUI launcher is required.");
    var script = File.ReadAllText(launcher);

    Assert.Contains("Read-Host -Prompt 'Hevy API key' -AsSecureString", script, StringComparison.Ordinal);
    Assert.Contains("[System.Management.Automation.PSCredential]::new", script, StringComparison.Ordinal);
    Assert.Contains("$credential.GetNetworkCredential().Password", script, StringComparison.Ordinal);
    Assert.Contains("[Environment]::SetEnvironmentVariable('HEVY_API_KEY', $plainKey, 'Process')", script, StringComparison.Ordinal);
    Assert.Contains("& $ClientPath", script, StringComparison.Ordinal);
    Assert.Contains("finally", script, StringComparison.Ordinal);
    Assert.Contains("[Environment]::SetEnvironmentVariable('HEVY_API_KEY', $previousKey, 'Process')", script, StringComparison.Ordinal);
    Assert.DoesNotContain("'User'", script, StringComparison.Ordinal);
    Assert.DoesNotContain("'Machine'", script, StringComparison.Ordinal);
    Assert.DoesNotContain("Set-Content", script, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("Out-File", script, StringComparison.OrdinalIgnoreCase);
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
    Assert.Equal(0, started.ExitCode);
    var containerId = started.StandardOutput.Trim();

    try
    {
      var port = await WaitForLoopbackPortAsync(containerId);
      var processOwner = await DockerProcess.RunAsync(["top", containerId, "-eo", "uid,pid"]);
      Assert.Equal(0, processOwner.ExitCode);
      Assert.Equal("1654", processOwner.StandardOutput
          .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
          .Last()
          .Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]);
      using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
      using var health = await WaitForHealthAsync(client);
      Assert.Equal(HttpStatusCode.OK, health.StatusCode);
      Assert.Equal(string.Empty, await health.Content.ReadAsStringAsync());

      using var mcp = await client.PostAsync("/mcp", new StringContent("{}"));
      Assert.Equal(HttpStatusCode.Unauthorized, mcp.StatusCode);
      Assert.Equal("Bearer", mcp.Headers.WwwAuthenticate.Single().Scheme);
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
      Assert.Fail($"The MCP process produced no protocol response; {error}");
    }
    return JsonDocument.Parse(line);
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
  [Theory]
  [InlineData(false, DockerAvailabilityDecision.Skip)]
  [InlineData(true, DockerAvailabilityDecision.Fail)]
  public void MissingDockerExecutableSkipsOnlyOutsideCi(bool isCi, DockerAvailabilityDecision expected)
  {
    var probe = new DockerProbeResult(127, string.Empty, "docker executable was not found", ExecutableMissing: true);

    Assert.Equal(expected, DockerAvailabilityPolicy.Evaluate(probe, isCi));
  }

  [Theory]
  [InlineData(false, DockerAvailabilityDecision.Skip)]
  [InlineData(true, DockerAvailabilityDecision.Fail)]
  public void RecognizedStoppedLocalDaemonSkipsOnlyOutsideCi(bool isCi, DockerAvailabilityDecision expected)
  {
    var probe = new DockerProbeResult(
        1,
        string.Empty,
        "Cannot connect to the Docker daemon at unix:///var/run/docker.sock. Is the docker daemon running?",
        ExecutableMissing: false);

    Assert.Equal(expected, DockerAvailabilityPolicy.Evaluate(probe, isCi));
  }

  [Theory]
  [InlineData("permission denied while trying to connect to the Docker daemon socket")]
  [InlineData("remote error: tls: bad certificate")]
  [InlineData("context named production does not exist")]
  [InlineData("client API version is too old")]
  [InlineData("arbitrary exit one")]
  public void ArbitraryDockerFailuresNeverSkip(string error)
  {
    var probe = new DockerProbeResult(1, string.Empty, error, ExecutableMissing: false);

    Assert.Equal(DockerAvailabilityDecision.Fail, DockerAvailabilityPolicy.Evaluate(probe, isCi: false));
    Assert.Equal(DockerAvailabilityDecision.Fail, DockerAvailabilityPolicy.Evaluate(probe, isCi: true));
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
        Assert.True(builtTags.TryAdd(tag, 0), $"Build tag collided: {tag}");
        return new DockerCommandResult(0, string.Empty, string.Empty, ExecutableMissing: false);
      }

      if (arguments is ["image", "inspect", "--format", _, var inspectedTag])
      {
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(inspectedTag))).ToLowerInvariant();
        return new DockerCommandResult(0, $"sha256:{digest}\n", string.Empty, ExecutableMissing: false);
      }

      if (arguments is ["image", "rm", var removedTag])
      {
        Assert.True(removedTags.TryAdd(removedTag, 0), $"Tag was cleaned more than once: {removedTag}");
        return new DockerCommandResult(0, string.Empty, string.Empty, ExecutableMissing: false);
      }

      throw new InvalidOperationException($"Unexpected fake Docker command: {string.Join(' ', arguments)}");
    }

    await using var first = new ContainerImageCoordinator(FakeDocker);
    await using var second = new ContainerImageCoordinator(FakeDocker);

    var ids = await Task.WhenAll(
        first.EnsureBuiltAsync(DockerProcess.RepositoryRoot),
        second.EnsureBuiltAsync(DockerProcess.RepositoryRoot));

    Assert.NotEqual(first.OwnedTag, second.OwnedTag);
    Assert.Matches("^hevy-client:container-smoke-[0-9]+-[0-9a-f]{32}$", first.OwnedTag);
    Assert.Matches("^sha256:[0-9a-f]{64}$", first.ImmutableImageId);
    Assert.Equal(ids[0], first.ImmutableImageId);
    Assert.Equal(ids[1], second.ImmutableImageId);
    Assert.NotEqual(ids[0], ids[1]);

    await first.DisposeAsync();
    await second.DisposeAsync();
    Assert.Equal(
        new[] { first.OwnedTag, second.OwnedTag }.Order(StringComparer.Ordinal).ToArray(),
        removedTags.Keys.Order(StringComparer.Ordinal).ToArray());
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
    await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.EnsureBuiltAsync(DockerProcess.RepositoryRoot));

    await coordinator.DisposeAsync();

    Assert.Equal(coordinator.OwnedTag, removedTag);
  }

  [Fact]
  public void PublicDistributionIsBlockedUntilPrivateSecurityReportingIsEnabled()
  {
    var security = File.ReadAllText(Path.Combine(DockerProcess.RepositoryRoot, "SECURITY.md"));
    var checklistPath = Path.Combine(DockerProcess.RepositoryRoot, "docs", "release-checklist.md");

    Assert.Contains("](../../security/advisories/new)", security, StringComparison.Ordinal);
    Assert.Contains("must be enabled before public distribution", security, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("Do not open a public issue for a suspected vulnerability", security, StringComparison.Ordinal);
    Assert.True(File.Exists(checklistPath), "The public-distribution release checklist is required.");

    var checklist = File.ReadAllText(checklistPath);
    Assert.Contains("BLOCK RELEASE", checklist, StringComparison.Ordinal);
    Assert.Contains("private vulnerability reporting", checklist, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("../../security/advisories/new", checklist, StringComparison.Ordinal);
  }

  [Fact]
  public void DesktopClientDocumentationUsesOperationalSecretBackedLaunchers()
  {
    var readme = File.ReadAllText(Path.Combine(DockerProcess.RepositoryRoot, "README.md"));

    Assert.Contains("scripts/hevy-client-mcp", readme, StringComparison.Ordinal);
    Assert.Contains("scripts/Start-HevyClient.ps1", readme, StringComparison.Ordinal);
    Assert.Contains("macOS Keychain", readme, StringComparison.Ordinal);
    Assert.Contains("Linux Secret Service", readme, StringComparison.Ordinal);
    Assert.Contains("Windows", readme, StringComparison.Ordinal);
    Assert.Contains("\"command\": \"/absolute/path/to/hevy-client-mcp\"", readme, StringComparison.Ordinal);
    Assert.DoesNotContain("restart a desktop client after setting it", readme, StringComparison.OrdinalIgnoreCase);
  }
}
