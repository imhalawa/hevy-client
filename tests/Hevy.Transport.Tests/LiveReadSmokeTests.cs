using Hevy.Client;
using Xunit;

namespace Hevy.Transport.Tests;

public sealed class LiveReadSmokeTests
{
  [LiveReadFact]
  public async Task AuthenticatedUserInfoReadRequiresExplicitLiveGate()
  {
    var client = new HevyClient(new HevyClientOptions(Environment.GetEnvironmentVariable("HEVY_API_KEY")!));

    var user = await client.GetUserInfoAsync(CancellationToken.None);

    (string.IsNullOrWhiteSpace(user.Id)).Should().BeFalse();
  }

  [Theory]
  [InlineData(null, null)]
  [InlineData("false", "present-key")]
  [InlineData("True", "present-key")]
  [InlineData("true", null)]
  [InlineData("true", "  ")]
  public void MissingOrInexactReadGateStopsBeforeRequest(string? liveTests, string? apiKey)
  {
    var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
    {
      ["HEVY_LIVE_TESTS"] = liveTests,
      ["HEVY_API_KEY"] = apiKey,
    };
    var gate = LiveTestGate.Evaluate(environment.GetValueOrDefault, mutation: false);

    (gate.Enabled).Should().BeFalse();
    (gate.SkipReason).Should().Contain("HEVY_LIVE_TESTS=true");
  }
}

internal sealed record LiveTestGateResult(bool Enabled, string SkipReason);

internal static class LiveTestGate
{
  internal static LiveTestGateResult Evaluate(Func<string, string?> getEnvironmentVariable, bool mutation)
  {

    var readEnabled = string.Equals(getEnvironmentVariable("HEVY_LIVE_TESTS"), "true", StringComparison.Ordinal);
    var mutationEnabled = !mutation ||
        string.Equals(getEnvironmentVariable("HEVY_LIVE_MUTATION_TESTS"), "true", StringComparison.Ordinal);
    var hasApiKey = !string.IsNullOrWhiteSpace(getEnvironmentVariable("HEVY_API_KEY"));
    var enabled = readEnabled && mutationEnabled && hasApiKey;
    var reason = mutation
        ? "Skipped: set HEVY_LIVE_TESTS=true and HEVY_LIVE_MUTATION_TESTS=true and provide a non-empty existing HEVY_API_KEY to run live mutation tests."
        : "Skipped: set HEVY_LIVE_TESTS=true and provide a non-empty existing HEVY_API_KEY to run live read tests.";
    return new LiveTestGateResult(enabled, reason);
  }
}

internal sealed class LiveReadFactAttribute : FactAttribute
{
  public LiveReadFactAttribute()
  {
    var result = LiveTestGate.Evaluate(Environment.GetEnvironmentVariable, mutation: false);
    if (!result.Enabled)
    {
      Skip = result.SkipReason;
    }
  }
}

internal sealed class LiveMutationFactAttribute : FactAttribute
{
  public LiveMutationFactAttribute()
  {
    var result = LiveTestGate.Evaluate(Environment.GetEnvironmentVariable, mutation: true);
    if (!result.Enabled)
    {
      Skip = result.SkipReason;
    }
  }
}
