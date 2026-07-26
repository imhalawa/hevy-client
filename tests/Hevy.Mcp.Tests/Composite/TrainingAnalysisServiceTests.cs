using System.Text.Json;
using Hevy.Client.Models;
using Hevy.Mcp.Composite;
using TestSupport;
using Xunit;

namespace Hevy.Mcp.Tests.Composite;

public sealed class TrainingAnalysisServiceTests
{
  private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-27T00:00:00Z");

  [Fact]
  public async Task WorkoutEvidenceIsBoundedAndContinuationPreservesTheUtcRange()
  {
    var workouts = new[] { Workout("workout-1", "2026-07-21T10:00:00Z", 100), Workout("workout-2", "2026-07-14T10:00:00Z", 105) };
    var client = new FakeHevyClient
    {
      GetWorkoutsHandler = (page, pageSize, _) => Task.FromResult(new PagedResult<Workout>(page, 2, [workouts[page - 1]])),
    };
    var service = new TrainingAnalysisService(client, new FixedTimeProvider(Now));

    var first = await service.GetWorkoutEvidenceAsync(4, null, 1, null, default);
    var second = await service.GetWorkoutEvidenceAsync(4, null, 1, first.Continuation, default);

    Assert.Equal("workout-1", Assert.Single(first.Items).WorkoutId);
    Assert.True(first.Truncated);
    Assert.NotNull(first.Continuation);
    Assert.Equal(first.RangeStartUtc, second.RangeStartUtc);
    Assert.Equal(first.RangeEndUtc, second.RangeEndUtc);
    Assert.Equal("workout-2", Assert.Single(second.Items).WorkoutId);
    Assert.False(second.Truncated);
  }

  [Fact]
  public async Task TrainingSummaryCalculatesUtcWeeksVolumeProgressionGapsAndMeasurementDeltas()
  {
    var workouts = new[]
    {
      Workout("workout-3", "2026-07-21T10:00:00Z", 110),
      Workout("workout-2", "2026-07-07T10:00:00Z", 105),
      Workout("workout-1", "2026-06-30T10:00:00Z", 100),
    };
    var client = new FakeHevyClient
    {
      Workouts = new(1, 1, workouts),
      BodyMeasurements = new(1, 1,
      [
        FakeHevyClient.SampleMeasurement() with { Date = new DateOnly(2026, 6, 30), WeightKg = 80, LeftBicepCm = 34 },
        FakeHevyClient.SampleMeasurement() with { Date = new DateOnly(2026, 7, 21), WeightKg = 79, LeftBicepCm = 35 },
      ]),
    };
    var service = new TrainingAnalysisService(client, new FixedTimeProvider(Now));

    var result = await service.SummarizeTrainingAsync(4, null, 100, null, default);

    Assert.Equal(3, result.WorkoutFrequency);
    Assert.Equal([1, 1, 0, 1], result.WeeklyFrequency.Select(static week => week.WorkoutCount));
    var squat = Assert.Single(result.Exercises);
    Assert.Equal(1_575m, squat.VolumeKgReps);
    Assert.Equal(50m, squat.ProgressionKgReps);
    Assert.Equal(["workout-1", "workout-2", "workout-3"], squat.Evidence.Select(static evidence => evidence.WorkoutId));
    Assert.Equal(
        [DateTimeOffset.Parse("2026-06-30T10:00:00Z"), DateTimeOffset.Parse("2026-07-07T10:00:00Z"), DateTimeOffset.Parse("2026-07-21T10:00:00Z")],
        squat.Evidence.Select(static evidence => evidence.StartTime));
    Assert.Equal(new DateOnly(2026, 7, 13), Assert.Single(result.MissingWeekGaps).WeekStartUtc);
    var weight = Assert.Single(result.MeasurementDeltas, delta => delta.Metric == "weight_kg");
    Assert.Equal(-1m, weight.Delta);
    Assert.Equal([new DateOnly(2026, 6, 30), new DateOnly(2026, 7, 21)], weight.EvidenceDates);
    Assert.Equal(1m, Assert.Single(result.MeasurementDeltas, delta => delta.Metric == "left_bicep_cm").Delta);
  }

  [Fact]
  public async Task VolumeExcludesSetsMissingWeightOrRepsAndOutputContainsNoCoachingLanguage()
  {
    var workout = Workout("workout-1", "2026-07-21T10:00:00Z", 100) with
    {
      Exercises =
      [
        new WorkoutExercise(0, "Squat", "", "template-1", null,
        [
          new WorkoutSet(0, "normal", 100, 5, null, null, null, null),
          new WorkoutSet(1, "normal", null, 5, null, null, null, null),
          new WorkoutSet(2, "normal", 100, null, null, null, null, null),
        ]),
      ],
    };
    var client = new FakeHevyClient { Workouts = new(1, 1, [workout]) };
    var result = await new TrainingAnalysisService(client, new FixedTimeProvider(Now)).SummarizeTrainingAsync(4, null, 100, null, default);
    var json = JsonSerializer.Serialize(result).ToLowerInvariant();

    Assert.Equal(500m, Assert.Single(result.Exercises).VolumeKgReps);
    foreach (var subjective in new[] { "good", "bad", "strong", "weak", "recommend", "should", "coach" })
    {
      Assert.DoesNotContain(subjective, json, StringComparison.Ordinal);
    }
  }

  [Fact]
  public async Task ExerciseHistorySummaryIsChronologicalAndReturnsEvidenceIdentifiers()
  {
    var client = new FakeHevyClient
    {
      ExerciseHistory = new(1, 1,
      [
        History("workout-2", "2026-07-20T10:00:00Z", 110, 5),
        History("workout-1", "2026-07-01T10:00:00Z", 100, 5),
      ]),
    };
    var service = new TrainingAnalysisService(client, new FixedTimeProvider(Now));

    var result = await service.SummarizeExerciseHistoryAsync("template-1", 4, null, 100, null, default);

    Assert.Equal(1_050m, result.VolumeKgReps);
    Assert.Equal(50m, result.ProgressionKgReps);
    Assert.Equal(["workout-1", "workout-2"], result.Evidence.Select(static evidence => evidence.WorkoutId));
    Assert.False(result.Truncated);
  }

  [Theory]
  [InlineData(0)]
  [InlineData(53)]
  public async Task SummariesRejectRangesOutsideOneThroughFiftyTwoWeeks(int weeks)
  {
    var service = new TrainingAnalysisService(new FakeHevyClient(), new FixedTimeProvider(Now));

    await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SummarizeTrainingAsync(weeks, null, 100, null, default));
  }

  [Fact]
  public async Task DefaultRangeIsFourCompleteUtcWeeksEndingAtTheNextMondayBoundary()
  {
    var service = new TrainingAnalysisService(new FakeHevyClient(), new FixedTimeProvider(DateTimeOffset.Parse("2026-07-26T18:30:00+02:00")));

    var result = await service.SummarizeTrainingAsync(null, null, 100, null, default);

    Assert.Equal(4, result.Weeks);
    Assert.Equal(DateTimeOffset.Parse("2026-06-29T00:00:00Z"), result.RangeStartUtc);
    Assert.Equal(DateTimeOffset.Parse("2026-07-27T00:00:00Z"), result.RangeEndUtc);
  }

  [Fact]
  public async Task EmptyUpstreamPagesStillConsumeTheBoundedPerCallScanBudget()
  {
    var client = new FakeHevyClient
    {
      GetWorkoutsHandler = (page, _, _) => Task.FromResult(new PagedResult<Workout>(page, 101, [])),
    };
    var service = new TrainingAnalysisService(client, new FixedTimeProvider(Now));

    var result = await service.GetWorkoutEvidenceAsync(4, null, 1_000, null, default);

    Assert.Equal(100, client.CallCount);
    Assert.True(result.Truncated);
    Assert.NotNull(result.Continuation);
  }

  private static Workout Workout(string id, string start, decimal weight) => FakeHevyClient.SampleWorkout() with
  {
    Id = id,
    StartTime = DateTimeOffset.Parse(start),
    EndTime = DateTimeOffset.Parse(start).AddHours(1),
    Exercises = [new WorkoutExercise(0, "Squat", "", "template-1", null, [new WorkoutSet(0, "normal", weight, 5, null, null, null, null)])],
  };

  private static ExerciseHistoryEntry History(string workoutId, string start, decimal weight, decimal reps) =>
      new(workoutId, "Workout", DateTimeOffset.Parse(start), DateTimeOffset.Parse(start).AddHours(1), "template-1", weight, reps, null, null, null, null, "normal");

  private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
  {
    public override DateTimeOffset GetUtcNow() => now;
  }
}
