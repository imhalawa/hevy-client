using System.Diagnostics;
using Xunit;

namespace Hevy.Transport.Tests;

public sealed class ProcessRunnerTests
{
  [Fact]
  public async Task RunnerDrainsLargeStandardOutputAndErrorConcurrently()
  {
    var result = await DeliveryContractTests.RunProcessAsync(
        DockerProcess.RepositoryRoot,
        "/bin/sh",
        "-c",
        "head -c 262144 /dev/zero | tr '\\0' O & head -c 262144 /dev/zero | tr '\\0' E >&2 & wait");

    (result.ExitCode).Should().Be(0);
    (result.StandardOutput).Should().Be(new string('O', 262_144));
    (result.StandardError).Should().Be(new string('E', 262_144));
  }

  [Fact]
  public async Task RunnerKillsSleepingProcessTreeAfterTimeout()
  {
    var started = Stopwatch.GetTimestamp();

    await FluentActions.Awaiting(() => DeliveryContractTests.RunProcessAsync(
        DockerProcess.RepositoryRoot,
        "/bin/sh",
        environment: null,
        TimeSpan.FromMilliseconds(200),
        "-c",
        "sleep 30 & wait")).Should().ThrowExactlyAsync<TimeoutException>();

    Stopwatch.GetElapsedTime(started).Should().BeLessThan(TimeSpan.FromSeconds(5));
  }
}
