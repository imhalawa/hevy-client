using System.Text.Json;
using Hevy.Client;
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

    Assert.Equal(3, result.ChunkWorkoutFrequency);
    Assert.Equal([1, 1, 0, 1], result.WeeklyFrequency.Select(static week => week.ChunkWorkoutCount));
    var squat = Assert.Single(result.Exercises);
    Assert.Equal(1_575m, squat.ChunkVolumeKgReps);
    Assert.Equal(50m, squat.ChunkProgressionKgReps);
    Assert.Equal(["workout-1", "workout-2", "workout-3"], squat.Evidence.Select(static evidence => evidence.WorkoutId));
    Assert.Equal(
        [DateTimeOffset.Parse("2026-06-30T10:00:00Z"), DateTimeOffset.Parse("2026-07-07T10:00:00Z"), DateTimeOffset.Parse("2026-07-21T10:00:00Z")],
        squat.Evidence.Select(static evidence => evidence.StartTime));
    Assert.Equal(DateTimeOffset.Parse("2026-07-13T00:00:00Z"), Assert.Single(result.MissingWeekGaps).PeriodStartUtc);
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

    Assert.Equal(500m, Assert.Single(result.Exercises).ChunkVolumeKgReps);
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

    Assert.Equal(1_050m, result.ChunkVolumeKgReps);
    Assert.Equal(50m, result.ChunkProgressionKgReps);
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

  [Theory]
  [InlineData(1)]
  [InlineData(2)]
  [InlineData(3)]
  [InlineData(4)]
  [InlineData(5)]
  [InlineData(6)]
  [InlineData(7)]
  [InlineData(8)]
  [InlineData(9)]
  [InlineData(10)]
  public async Task EveryPageSizeKeepsScannedCapacityWithinOneThousandPerInvocation(int pageSize)
  {
    var client = new FakeHevyClient
    {
      GetWorkoutsHandler = (page, _, _) => Task.FromResult(new PagedResult<Workout>(page, 2_000, [])),
    };
    var service = new TrainingAnalysisService(client, new FixedTimeProvider(Now));

    var result = await service.GetWorkoutEvidenceAsync(4, null, pageSize, null, default);

    Assert.InRange(client.CallCount * pageSize, 1, 1_000);
    Assert.True(result.Truncated);
    Assert.NotNull(result.Continuation);
  }

  [Fact]
  public async Task ExerciseHistoryMakesOneUnpaginatedUpstreamFetchPerCompositeInvocation()
  {
    var client = new FakeHevyClient
    {
      AllExerciseHistory =
      [
        History("workout-1", "2026-07-11T10:00:00Z", 101, 5),
        History("workout-2", "2026-07-12T10:00:00Z", 102, 5),
        History("workout-3", "2026-07-13T10:00:00Z", 103, 5),
      ],
    };
    var service = new TrainingAnalysisService(client, new FixedTimeProvider(Now));

    var result = await service.SummarizeExerciseHistoryAsync("template-1", 4, null, 2, null, default);

    Assert.Equal(1, client.CallCount);
    Assert.Equal(nameof(IHevyClient.GetAllExerciseHistoryAsync), client.LastOperation);
    Assert.True(result.Truncated);
    Assert.NotNull(result.Continuation);
  }

  [Theory]
  [InlineData(100, 150)]
  [InlineData(1_000, 1_050)]
  public async Task ExerciseHistoryAggregatesRequestedWindowFromOneAllHistoryFetch(int limit, int entryCount)
  {
    var start = DateTimeOffset.Parse("2026-07-01T00:00:00Z");
    var history = Enumerable.Range(1, entryCount)
        .Select(index => History($"workout-{index:D3}", start.AddMinutes(index).ToString("O"), 100 + index, 5))
        .ToArray();
    var client = new FakeHevyClient { AllExerciseHistory = history };
    var service = new TrainingAnalysisService(client, new FixedTimeProvider(Now));

    var first = await service.SummarizeExerciseHistoryAsync("template-1", 4, null, limit, null, default);
    var second = await service.SummarizeExerciseHistoryAsync("template-1", 4, null, limit, first.Continuation, default);

    Assert.Equal(limit, first.ChunkEntryCount);
    Assert.True(first.Truncated);
    Assert.NotNull(first.ContinuationInputs);
    Assert.Equal(entryCount - limit, second.ChunkEntryCount);
    Assert.False(second.Truncated);
    Assert.Equal(2, client.CallCount);
    Assert.Equal(nameof(IHevyClient.GetAllExerciseHistoryAsync), client.LastOperation);
  }

  [Fact]
  public async Task EachMeasurementMetricUsesItsOwnFirstAndLastNonNullEvidenceDates()
  {
    var client = new FakeHevyClient
    {
      Workouts = new(1, 0, []),
      BodyMeasurements = new(1, 1,
      [
        FakeHevyClient.SampleMeasurement() with { Date = new DateOnly(2026, 7, 1), LeftBicepCm = null },
        FakeHevyClient.SampleMeasurement() with { Date = new DateOnly(2026, 7, 10), LeftBicepCm = 34 },
        FakeHevyClient.SampleMeasurement() with { Date = new DateOnly(2026, 7, 20), LeftBicepCm = 35 },
      ]),
    };
    var result = await new TrainingAnalysisService(client, new FixedTimeProvider(Now)).SummarizeTrainingAsync(4, null, 100, null, default);

    var bicep = Assert.Single(result.MeasurementDeltas, delta => delta.Metric == "left_bicep_cm");
    Assert.Equal(1m, bicep.Delta);
    Assert.Equal([new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 20)], bicep.EvidenceDates);
  }

  [Fact]
  public async Task WorkoutEvidenceContinuationCannotBeUsedByTrainingSummaryAndRejectsBeforeIo()
  {
    var client = new FakeHevyClient
    {
      GetWorkoutsHandler = (page, _, _) => Task.FromResult(new PagedResult<Workout>(page, 2, [Workout($"workout-{page}", $"2026-07-{page + 20:D2}T10:00:00Z", 100)])),
    };
    var service = new TrainingAnalysisService(client, new FixedTimeProvider(Now));
    var evidence = await service.GetWorkoutEvidenceAsync(4, null, 1, null, default);
    var callsBefore = client.CallCount;

    await Assert.ThrowsAsync<ArgumentException>(() => service.SummarizeTrainingAsync(4, null, 1, evidence.Continuation, default));
    Assert.Equal(callsBefore, client.CallCount);
  }

  [Fact]
  public async Task DefaultRangeContinuationRemainsStableAfterCrossingMondayBoundary()
  {
    var clock = new MutableTimeProvider(DateTimeOffset.Parse("2026-07-26T23:59:00Z"));
    var client = new FakeHevyClient
    {
      GetWorkoutsHandler = (page, _, _) => Task.FromResult(new PagedResult<Workout>(page, 2, [Workout($"workout-{page}", $"2026-07-{page + 20:D2}T10:00:00Z", 100)])),
    };
    var service = new TrainingAnalysisService(client, clock);
    var first = await service.GetWorkoutEvidenceAsync(4, null, 1, null, default);
    clock.Advance(TimeSpan.FromDays(1));

    var second = await service.GetWorkoutEvidenceAsync(4, null, 1, first.Continuation, default);

    Assert.Equal(first.RangeStartUtc, second.RangeStartUtc);
    Assert.Equal(first.RangeEndUtc, second.RangeEndUtc);
  }

  [Fact]
  public async Task WeeklyBucketsUseExactHalfOpenUtcInstantsForNonMidnightRange()
  {
    var end = DateTimeOffset.Parse("2026-07-27T12:00:00Z");
    var client = new FakeHevyClient
    {
      Workouts = new(1, 1,
      [
        Workout("workout-first", "2026-06-29T13:00:00Z", 100),
        Workout("workout-last", "2026-07-27T11:00:00Z", 100),
      ]),
    };
    var result = await new TrainingAnalysisService(client, new FixedTimeProvider(Now)).SummarizeTrainingAsync(4, end, 100, null, default);

    Assert.Equal(2, result.WeeklyFrequency.Sum(static week => week.ChunkWorkoutCount));
  }

  [Fact]
  public async Task PartialSummaryChunksComposeToTheSamePeriodMetricsAsOneCompleteCall()
  {
    var end = DateTimeOffset.Parse("2026-07-27T00:00:00Z");
    var workouts = new[]
    {
      Workout("workout-1", "2026-06-30T10:00:00Z", 100),
      Workout("workout-2", "2026-07-01T10:00:00Z", 102),
      Workout("workout-3", "2026-07-07T10:00:00Z", 104),
      Workout("workout-4", "2026-07-08T10:00:00Z", 106),
      Workout("workout-5", "2026-07-21T10:00:00Z", 108),
      Workout("workout-6", "2026-07-22T10:00:00Z", 110),
    };
    var measurements = new[]
    {
      FakeHevyClient.SampleMeasurement() with { Date = new DateOnly(2026, 7, 1), WeightKg = 80 },
      FakeHevyClient.SampleMeasurement() with { Date = new DateOnly(2026, 7, 10), WeightKg = null },
      FakeHevyClient.SampleMeasurement() with { Date = new DateOnly(2026, 7, 20), WeightKg = 79 },
    };
    var client = new FakeHevyClient
    {
      GetWorkoutsHandler = (page, pageSize, _) => Task.FromResult(Page(workouts, page, pageSize)),
      GetBodyMeasurementsHandler = (page, pageSize, _) => Task.FromResult(Page(measurements, page, pageSize)),
    };
    var service = new TrainingAnalysisService(client, new FixedTimeProvider(Now));
    var complete = await service.SummarizeTrainingAsync(4, end, 100, null, default);
    var chunks = new List<TrainingSummary>();
    string? continuation = null;
    do
    {
      var chunk = await service.SummarizeTrainingAsync(4, end, 2, continuation, default);
      chunks.Add(chunk);
      if (chunk.Truncated)
      {
        Assert.Equal("partial_chunk", chunk.MetricScope);
        Assert.NotNull(chunk.ContinuationInputs);
        Assert.Equal(4, chunk.ContinuationInputs.Weeks);
        Assert.Equal(end, chunk.ContinuationInputs.RangeEndUtc);
        Assert.Equal(2, chunk.ContinuationInputs.Limit);
      }
      continuation = chunk.Continuation;
    }
    while (continuation is not null);

    Assert.Equal("complete_period", complete.MetricScope);
    Assert.True(complete.GapsComplete);
    Assert.Equal(complete.ChunkWorkoutFrequency, chunks.Sum(static chunk => chunk.ChunkWorkoutFrequency));
    foreach (var fullWeek in complete.WeeklyFrequency)
    {
      Assert.Equal(fullWeek.ChunkWorkoutCount, chunks.Sum(chunk => chunk.WeeklyFrequency.Single(week => week.PeriodStartUtc == fullWeek.PeriodStartUtc).ChunkWorkoutCount));
    }
    var fullExercise = Assert.Single(complete.Exercises);
    Assert.Equal(fullExercise.ChunkVolumeKgReps, chunks.SelectMany(static chunk => chunk.Exercises).Sum(static exercise => exercise.ChunkVolumeKgReps));
    var observations = chunks.SelectMany(static chunk => chunk.Exercises)
        .SelectMany(static exercise => new[] { exercise.FirstObservation, exercise.LastObservation })
        .OrderBy(static observation => observation.StartTime).ToArray();
    Assert.Equal(fullExercise.ChunkProgressionKgReps, observations[^1].VolumeKgReps - observations[0].VolumeKgReps);
    var composedGapStarts = complete.WeeklyFrequency
        .Where(fullWeek => chunks.Sum(chunk => chunk.WeeklyFrequency.Single(week => week.PeriodStartUtc == fullWeek.PeriodStartUtc).ChunkWorkoutCount) == 0)
        .Select(static week => week.PeriodStartUtc).ToArray();
    Assert.Equal(complete.MissingWeekGaps.Select(static gap => gap.PeriodStartUtc), composedGapStarts);
    Assert.All(chunks.Where(static chunk => !chunk.GapsComplete), static chunk => Assert.Empty(chunk.MissingWeekGaps));
    var weightSamples = chunks.SelectMany(static chunk => chunk.MeasurementDeltas)
        .Where(static delta => delta.Metric == "weight_kg")
        .SelectMany(static delta => new[] { (Date: delta.EvidenceDates[0], Value: delta.FirstValue), (Date: delta.EvidenceDates[1], Value: delta.LastValue) })
        .OrderBy(static sample => sample.Date).ToArray();
    var fullWeight = Assert.Single(complete.MeasurementDeltas, static delta => delta.Metric == "weight_kg");
    Assert.Equal(fullWeight.Delta, weightSamples[^1].Value - weightSamples[0].Value);
  }

  [Fact]
  public async Task MoreThanOneThousandMeasurementsReturnPartialContinuationInsteadOfThrowing()
  {
    var measurement = FakeHevyClient.SampleMeasurement() with { Date = new DateOnly(2026, 7, 10) };
    var client = new FakeHevyClient
    {
      Workouts = new(1, 0, []),
      GetBodyMeasurementsHandler = (page, pageSize, _) => Task.FromResult(new PagedResult<BodyMeasurement>(page, 101, Enumerable.Repeat(measurement, pageSize).ToArray())),
    };
    var service = new TrainingAnalysisService(client, new FixedTimeProvider(Now));

    var first = await service.SummarizeTrainingAsync(4, null, 1_000, null, default);
    var second = await service.SummarizeTrainingAsync(4, null, 1_000, first.Continuation, default);

    Assert.True(first.Truncated);
    Assert.Equal("partial_chunk", first.MetricScope);
    Assert.NotNull(first.ContinuationInputs);
    Assert.False(second.Truncated);
    Assert.Equal("partial_chunk", second.MetricScope);
    Assert.Equal(102, client.CallCount);
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

  private static PagedResult<T> Page<T>(IReadOnlyList<T> items, int page, int pageSize)
  {
    var pageCount = items.Count == 0 ? 0 : (items.Count + pageSize - 1) / pageSize;
    return new PagedResult<T>(page, pageCount, items.Skip((page - 1) * pageSize).Take(pageSize).ToArray());
  }

  private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
  {
    public override DateTimeOffset GetUtcNow() => now;
  }

  private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
  {
    private DateTimeOffset _now = now;
    public override DateTimeOffset GetUtcNow() => _now;
    internal void Advance(TimeSpan duration) => _now += duration;
  }
}
