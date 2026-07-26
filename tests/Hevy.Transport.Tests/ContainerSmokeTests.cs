using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Xunit;
using Xunit.Sdk;

namespace Hevy.Transport.Tests;

[Collection("container-smoke")]
public sealed class ContainerSmokeTests
{
  private const string ImageName = "hevy-client:container-smoke";
  private static readonly SemaphoreSlim ImageBuildLock = new(1, 1);
  private static bool imageBuilt;

  [Fact]
  public async Task ImageIsPinnedNonRootReadOnlyFriendlyAndDoesNotAdvertiseAPort()
  {
    await EnsureImageAsync();

    var inspection = await RunDockerAsync(["image", "inspect", ImageName]);
    Assert.Equal(0, inspection.ExitCode);
    using var document = JsonDocument.Parse(inspection.StandardOutput);
    var config = document.RootElement[0].GetProperty("Config");

    Assert.Equal("app", config.GetProperty("User").GetString());
    Assert.Equal(["dotnet", "Hevy.Mcp.dll"], config.GetProperty("Entrypoint").EnumerateArray().Select(static value => value.GetString()!).ToArray());
    Assert.False(config.TryGetProperty("ExposedPorts", out var exposedPorts) && exposedPorts.ValueKind is not JsonValueKind.Null);

    var labels = config.GetProperty("Labels");
    Assert.Equal("MIT", labels.GetProperty("org.opencontainers.image.licenses").GetString());
    Assert.Equal("hevy-client", labels.GetProperty("org.opencontainers.image.title").GetString());
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

    var shellAttempt = await RunDockerAsync(["run", "--rm", "--entrypoint", "/bin/sh", ImageName, "-c", "exit 0"]);
    Assert.NotEqual(0, shellAttempt.ExitCode);
    var packageManagerAttempt = await RunDockerAsync(["run", "--rm", "--entrypoint", "/usr/bin/apt-get", ImageName, "--version"]);
    Assert.NotEqual(0, packageManagerAttempt.ExitCode);
  }

  [Fact]
  public async Task StdioContainerCompletesARealMcpHandshakeWithStdinAttached()
  {
    await EnsureImageAsync();
    using var process = StartDocker(
        "run", "--rm", "-i", "--read-only", "--tmpfs", "/tmp:rw,noexec,nosuid,size=16m",
        "-e", "HEVY_API_KEY=container-smoke-fixture-key", ImageName);

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

  [Fact]
  public async Task HttpContainerPublishesOnlyToLoopbackAndProtectsMcpWhileHealthIsEmpty()
  {
    await EnsureImageAsync();
    var started = await RunDockerAsync([
      "run", "--detach", "--rm", "--read-only", "--tmpfs", "/tmp:rw,noexec,nosuid,size=16m",
      "--publish", "127.0.0.1::8080",
      "-e", "HEVY_API_KEY=container-smoke-fixture-key",
      "-e", "HEVY_MCP_TRANSPORT=http",
      "-e", "MCP_AUTH_TOKEN=container-smoke-auth-token",
      "-e", "ASPNETCORE_URLS=http://0.0.0.0:8080",
      ImageName,
    ]);
    Assert.Equal(0, started.ExitCode);
    var containerId = started.StandardOutput.Trim();

    try
    {
      var port = await WaitForLoopbackPortAsync(containerId);
      var processOwner = await RunDockerAsync(["top", containerId, "-eo", "uid,pid"]);
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
      await RunDockerAsync(["rm", "--force", containerId]);
    }
  }

  private static async Task EnsureImageAsync()
  {
    var availability = await RunDockerAsync(["info"]);
    if (availability.ExitCode != 0)
    {
      throw SkipException.ForSkip($"Docker is genuinely unavailable: {availability.StandardError.Trim()}");
    }

    await ImageBuildLock.WaitAsync();
    try
    {
      if (imageBuilt)
      {
        return;
      }

      var repositoryRoot = FindRepositoryRoot();
      var build = await RunDockerAsync(
          ["build", "--pull", "--tag", ImageName, "."],
          repositoryRoot,
          TimeSpan.FromMinutes(10));
      Assert.True(build.ExitCode == 0, $"Container build failed.\nstdout:\n{build.StandardOutput}\nstderr:\n{build.StandardError}");
      imageBuilt = true;
    }
    finally
    {
      ImageBuildLock.Release();
    }
  }

  private static async Task<int> WaitForLoopbackPortAsync(string containerId)
  {
    for (var attempt = 0; attempt < 40; attempt++)
    {
      var portResult = await RunDockerAsync(["port", containerId, "8080/tcp"]);
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

  private static Process StartDocker(params string[] arguments)
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

  private static async Task<ProcessResult> RunDockerAsync(
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
      WorkingDirectory = workingDirectory ?? FindRepositoryRoot(),
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
      return new ProcessResult(process.ExitCode, await standardOutput, await standardError);
    }
    catch (System.ComponentModel.Win32Exception exception)
    {
      return new ProcessResult(127, string.Empty, exception.Message);
    }
  }

  private static async Task<JsonDocument> ReadProtocolMessageAsync(Process process)
  {
    var line = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(20));
    Assert.False(string.IsNullOrWhiteSpace(line));
    return JsonDocument.Parse(line);
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

  private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
