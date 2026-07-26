using Hevy.Client;
using Hevy.Client.Models;
using Xunit;

namespace Hevy.Transport.Tests;

public sealed class LiveMutationSmokeTests
{
  [LiveMutationFact]
  public async Task ExistingMeasurementCanBeReplacedWithoutChangingItsValuesWhenExplicitlyEnabled()
  {
    var client = new HevyClient(HevyClientOptions.FromEnvironment());
    var measurements = await client.GetBodyMeasurementsAsync(1, 1, CancellationToken.None);
    if (measurements.Items.Count == 0)
    {
      Assert.Fail("Live mutation smoke test requires one existing body measurement so it can perform a value-preserving replacement.");
    }

    var existing = measurements.Items[0];
    var request = new UpdateBodyMeasurementRequest(
        existing.WeightKg,
        existing.LeanMassKg,
        existing.FatPercent,
        existing.NeckCm,
        existing.ShoulderCm,
        existing.ChestCm,
        existing.LeftBicepCm,
        existing.RightBicepCm,
        existing.LeftForearmCm,
        existing.RightForearmCm,
        existing.Abdomen,
        existing.Waist,
        existing.Hips,
        existing.LeftThigh,
        existing.RightThigh,
        existing.LeftCalf,
        existing.RightCalf);

    var updated = await client.UpdateBodyMeasurementAsync(existing.Date, request, CancellationToken.None);

    Assert.Equal(existing.Date, updated.Date);
  }

  [Theory]
  [InlineData(null, null, null)]
  [InlineData("true", null, "present-key")]
  [InlineData("true", "false", "present-key")]
  [InlineData("true", "True", "present-key")]
  [InlineData("true", "true", null)]
  public void MissingOrInexactMutationGateStopsBeforeRequest(string? liveTests, string? liveMutations, string? apiKey)
  {
    var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
    {
      ["HEVY_LIVE_TESTS"] = liveTests,
      ["HEVY_LIVE_MUTATION_TESTS"] = liveMutations,
      ["HEVY_API_KEY"] = apiKey,
    };
    var requestCount = 0;

    var gate = LiveTestGate.Evaluate(environment.GetValueOrDefault, mutation: true);
    if (gate.Enabled)
    {
      requestCount++;
    }

    Assert.False(gate.Enabled);
    Assert.Contains("HEVY_LIVE_MUTATION_TESTS=true", gate.SkipReason, StringComparison.Ordinal);
    Assert.Equal(0, requestCount);
  }
}
