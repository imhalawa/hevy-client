using Hevy.Client;
using Xunit;

namespace Hevy.Transport.Tests;

public sealed class LiveMutationSmokeTests
{
  [LiveMutationFact]
  public async Task ExistingMeasurementCanBeReplacedWithoutChangingItsValuesWhenExplicitlyEnabled()
  {
    var client = new HevyClient(new HevyClientOptions(Environment.GetEnvironmentVariable("HEVY_API_KEY")!));
    var measurements = await client.GetBodyMeasurementsAsync(1, 1, CancellationToken.None);
    if (measurements.Items.Count == 0)
    {
      false.Should().BeTrue("Live mutation smoke test requires one existing body measurement so it can perform a value-preserving replacement.");
    }

    var existing = measurements.Items[0];
    var request = new UpdateBodyMeasurementCommand(new UpdateBodyMeasurementWrite(
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
        existing.RightCalf));

    var updated = await client.UpdateBodyMeasurementAsync(existing.Date, request, CancellationToken.None);

    PrivacySafeBodyMeasurementVerifier.Verify(existing, updated);
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

    (gate.Enabled).Should().BeFalse();
    (gate.SkipReason).Should().Contain("HEVY_LIVE_MUTATION_TESTS=true");
    (requestCount).Should().Be(0);
  }

  [Fact]
  public void MeasurementMismatchFailureContainsNoDateOrMeasurementValues()
  {
    var expected = new BodyMeasurement(
        new DateOnly(2099, 12, 31),
        12345.6789m,
        23456.7891m,
        98.7654m,
        34567.8912m,
        45678.9123m,
        56789.1234m,
        67891.2345m,
        78912.3456m,
        89123.4567m,
        91234.5678m,
        10234.5678m,
        11234.5678m,
        12234.5678m,
        13234.5678m,
        14234.5678m,
        15234.5678m,
        16234.5678m);
    var mismatches = new BodyMeasurement[]
    {
      expected with { Date = new DateOnly(2098, 11, 30) },
      expected with { WeightKg = 22345.6789m },
      expected with { LeanMassKg = 33456.7891m },
      expected with { FatPercent = 87.6543m },
      expected with { NeckCm = 44567.8912m },
      expected with { ShoulderCm = 55678.9123m },
      expected with { ChestCm = 66789.1234m },
      expected with { LeftBicepCm = 77891.2345m },
      expected with { RightBicepCm = 88912.3456m },
      expected with { LeftForearmCm = 99123.4567m },
      expected with { RightForearmCm = 81234.5678m },
      expected with { Abdomen = 70234.5678m },
      expected with { Waist = 61234.5678m },
      expected with { Hips = 52234.5678m },
      expected with { LeftThigh = 43234.5678m },
      expected with { RightThigh = 34234.5678m },
      expected with { LeftCalf = 25234.5678m },
      expected with { RightCalf = 17234.5678m },
    };

    foreach (var actual in mismatches)
    {
      var exception = FluentActions.Invoking(() => PrivacySafeBodyMeasurementVerifier.Verify(expected, actual)).Should().Throw<Xunit.Sdk.XunitException>().Which;

      (exception.Message).Should().Contain(PrivacySafeBodyMeasurementVerifier.FailureMessage);
      (exception.Message).Should().NotContain("2099-12-31");
      (exception.Message).Should().NotContain("12345.6789");
      (exception.Message).Should().NotContain("17234.5678");
    }
  }
}

internal static class PrivacySafeBodyMeasurementVerifier
{
  internal const string FailureMessage = "Live mutation response did not preserve every body-measurement field.";

  internal static void Verify(BodyMeasurement expected, BodyMeasurement actual)
  {
    if (!Matches(expected, actual))
    {
      false.Should().BeTrue(FailureMessage);
    }
  }

  private static bool Matches(BodyMeasurement expected, BodyMeasurement actual) =>
      expected.Date == actual.Date &&
      expected.WeightKg == actual.WeightKg &&
      expected.LeanMassKg == actual.LeanMassKg &&
      expected.FatPercent == actual.FatPercent &&
      expected.NeckCm == actual.NeckCm &&
      expected.ShoulderCm == actual.ShoulderCm &&
      expected.ChestCm == actual.ChestCm &&
      expected.LeftBicepCm == actual.LeftBicepCm &&
      expected.RightBicepCm == actual.RightBicepCm &&
      expected.LeftForearmCm == actual.LeftForearmCm &&
      expected.RightForearmCm == actual.RightForearmCm &&
      expected.Abdomen == actual.Abdomen &&
      expected.Waist == actual.Waist &&
      expected.Hips == actual.Hips &&
      expected.LeftThigh == actual.LeftThigh &&
      expected.RightThigh == actual.RightThigh &&
      expected.LeftCalf == actual.LeftCalf &&
      expected.RightCalf == actual.RightCalf;
}
