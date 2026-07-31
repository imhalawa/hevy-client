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

    ((first.Items).Should().ContainSingle().Which.WorkoutId).Should().Be("workout-1");
    (first.Truncated).Should().BeTrue();
    (first.Continuation).Should().NotBeNull();
    (second.RangeStartUtc).Should().Be(first.RangeStartUtc);
    (second.RangeEndUtc).Should().Be(first.RangeEndUtc);
    ((second.Items).Should().ContainSingle().Which.WorkoutId).Should().Be("workout-2");
    (second.Truncated).Should().BeFalse();
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

    (result.ChunkWorkoutFrequency).Should().Be(3);
    (result.WeeklyFrequency.Select(static week => week.ChunkWorkoutCount)).Should().Equal([1, 1, 0, 1]);
    var squat = (result.Exercises).Should().ContainSingle().Which;
    (squat.ChunkVolumeKgReps).Should().Be(1_575m);
    (squat.ChunkProgressionKgReps).Should().Be(50m);
    (squat.Evidence.Select(static evidence => evidence.WorkoutId)).Should().Equal(["workout-1", "workout-2", "workout-3"]);
    (squat.Evidence.Select(static evidence => evidence.StartTime)).Should().Equal([DateTimeOffset.Parse("2026-06-30T10:00:00Z"), DateTimeOffset.Parse("2026-07-07T10:00:00Z"), DateTimeOffset.Parse("2026-07-21T10:00:00Z")]);
    ((result.MissingWeekGaps).Should().ContainSingle().Which.PeriodStartUtc).Should().Be(DateTimeOffset.Parse("2026-07-13T00:00:00Z"));
    var weight = (result.MeasurementDeltas).Should().ContainSingle(delta => delta.Metric == "weight_kg").Which;
    (weight.Delta).Should().Be(-1m);
    (weight.EvidenceDates).Should().Equal([new DateOnly(2026, 6, 30), new DateOnly(2026, 7, 21)]);
    ((result.MeasurementDeltas).Should().ContainSingle(delta => delta.Metric == "left_bicep_cm").Which.Delta).Should().Be(1m);
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

    ((result.Exercises).Should().ContainSingle().Which.ChunkVolumeKgReps).Should().Be(500m);
    foreach (var subjective in new[] { "good", "bad", "strong", "weak", "recommend", "should", "coach" })
    {
      (json).Should().NotContain(subjective);
    }
  }

  [Fact]
  public async Task TrainingSummaryAggregatesRepeatedTemplateBlocksIntoOneWorkoutObservation()
  {
    var first = Workout("workout-1", "2026-07-07T10:00:00Z", 100) with
    {
      Exercises =
      [
        new WorkoutExercise(0, "Squat", "", "template-1", null, [new WorkoutSet(0, "normal", 100, 5, null, null, null, null)]),
        new WorkoutExercise(1, "Squat", "", "template-1", null, [new WorkoutSet(0, "normal", 50, 5, null, null, null, null)]),
      ],
    };
    var second = Workout("workout-2", "2026-07-21T10:00:00Z", 120);
    var client = new FakeHevyClient { Workouts = new(1, 1, [first, second]) };

    var result = await new TrainingAnalysisService(client, new FixedTimeProvider(Now)).SummarizeTrainingAsync(4, null, 100, null, default);

    var squat = (result.Exercises).Should().ContainSingle().Which;
    (squat.ChunkVolumeKgReps).Should().Be(1_350m);
    (squat.FirstObservation.VolumeKgReps).Should().Be(750m);
    (squat.LastObservation.VolumeKgReps).Should().Be(600m);
    (squat.ChunkProgressionKgReps).Should().Be(-150m);
    (squat.Evidence.Select(static item => item.WorkoutId)).Should().Equal(["workout-1", "workout-2"]);
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

    (result.ChunkVolumeKgReps).Should().Be(1_050m);
    (result.ChunkProgressionKgReps).Should().Be(50m);
    (result.Evidence.Select(static evidence => evidence.WorkoutId)).Should().Equal(["workout-1", "workout-2"]);
    (result.Truncated).Should().BeFalse();
  }

  [Theory]
  [InlineData(0)]
  [InlineData(53)]
  public async Task SummariesRejectRangesOutsideOneThroughFiftyTwoWeeks(int weeks)
  {
    var service = new TrainingAnalysisService(new FakeHevyClient(), new FixedTimeProvider(Now));

    await FluentActions.Awaiting(() => service.SummarizeTrainingAsync(weeks, null, 100, null, default)).Should().ThrowExactlyAsync<ArgumentOutOfRangeException>();
  }

  [Fact]
  public async Task DefaultRangeIsFourCompleteUtcWeeksEndingAtTheNextMondayBoundary()
  {
    var service = new TrainingAnalysisService(new FakeHevyClient(), new FixedTimeProvider(DateTimeOffset.Parse("2026-07-26T18:30:00+02:00")));

    var result = await service.SummarizeTrainingAsync(null, null, 100, null, default);

    (result.Weeks).Should().Be(4);
    (result.RangeStartUtc).Should().Be(DateTimeOffset.Parse("2026-06-29T00:00:00Z"));
    (result.RangeEndUtc).Should().Be(DateTimeOffset.Parse("2026-07-27T00:00:00Z"));
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

    (client.CallCount).Should().Be(100);
    (result.Truncated).Should().BeTrue();
    (result.Continuation).Should().NotBeNull();
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

    (client.CallCount * pageSize).Should().BeInRange(1, 1_000);
    (result.Truncated).Should().BeTrue();
    (result.Continuation).Should().NotBeNull();
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

    (client.CallCount).Should().Be(1);
    (client.LastOperation).Should().Be(nameof(IHevyClient.GetExerciseHistoryWindowAsync));
    (result.Truncated).Should().BeTrue();
    (result.Continuation).Should().NotBeNull();
  }

  [Fact]
  public async Task ExerciseHistoryAggregatesRequestedWindowFromOneStreamingFetch()
  {
    var start = DateTimeOffset.Parse("2026-07-01T00:00:00Z");
    var history = Enumerable.Range(1, 150)
        .Select(index => History($"workout-{index:D3}", start.AddMinutes(index).ToString("O"), 100 + index, 5))
        .ToArray();
    var client = new FakeHevyClient { AllExerciseHistory = history };
    var service = new TrainingAnalysisService(client, new FixedTimeProvider(Now));

    var first = await service.SummarizeExerciseHistoryAsync("template-1", 4, null, 100, null, default);
    var second = await service.SummarizeExerciseHistoryAsync("template-1", 4, null, 100, first.Continuation, default);

    (first.ChunkEntryCount).Should().Be(100);
    (first.ScannedEntryCount).Should().BeInRange(1, ExerciseHistoryWindowRequest.MaximumScannedItems);
    (first.Truncated).Should().BeTrue();
    (first.TruncationReason).Should().BeNull();
    (first.ContinuationInputs).Should().NotBeNull();
    (second.ChunkEntryCount).Should().Be(50);
    (second.Truncated).Should().BeFalse();
    (client.CallCount).Should().Be(2);
    (client.LastOperation).Should().Be(nameof(IHevyClient.GetExerciseHistoryWindowAsync));
  }

  [Fact]
  public async Task ExerciseHistoryLimitOneThousandReturnsAnExplicitTerminalSafetyTruncation()
  {
    var start = DateTimeOffset.Parse("2026-07-01T00:00:00Z");
    var history = Enumerable.Range(1, 1_050)
        .Select(index => History($"workout-{index:D4}", start.AddMinutes(index).ToString("O"), 100 + index, 5))
        .ToArray();
    var client = new FakeHevyClient { AllExerciseHistory = history };
    var service = new TrainingAnalysisService(client, new FixedTimeProvider(Now));

    var result = await service.SummarizeExerciseHistoryAsync("template-1", 4, null, 1_000, null, default);

    (result.ChunkEntryCount).Should().Be(1_000);
    (result.ScannedEntryCount).Should().Be(1_000);
    (result.Truncated).Should().BeTrue();
    (result.TruncationReason).Should().Be(ExerciseHistoryWindow.ItemSafetyCap);
    (result.Continuation).Should().BeNull();
    (result.ContinuationInputs).Should().BeNull();
    (client.CallCount).Should().Be(1);
  }

  [Fact]
  public async Task ExerciseHistoryRejectsAnUnrepresentableContinuationOffsetBeforeClientIo()
  {
    var client = new FakeHevyClient();
    var service = new TrainingAnalysisService(client, new FixedTimeProvider(Now));
    var continuation = Continuation.Create(
        "exercise-history-summary",
        int.MaxValue,
        new SortedDictionary<string, string?>(StringComparer.Ordinal)
        {
          ["end_utc"] = "2026-07-27T00:00:00.0000000+00:00",
          ["exercise_template_id"] = "template-1",
          ["limit"] = "100",
          ["page_size"] = "100",
          ["phase"] = "history",
          ["start_utc"] = "2026-06-29T00:00:00.0000000+00:00",
          ["weeks"] = "4",
        },
        Continuation.MaximumItemBudget);

    await FluentActions.Awaiting(() => service.SummarizeExerciseHistoryAsync(
        "template-1",
        4,
        DateTimeOffset.Parse("2026-07-27T00:00:00Z"),
        100,
        continuation,
        default)).Should().ThrowAsync<ArgumentException>();

    (client.CallCount).Should().Be(0);
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

    var bicep = (result.MeasurementDeltas).Should().ContainSingle(delta => delta.Metric == "left_bicep_cm").Which;
    (bicep.Delta).Should().Be(1m);
    (bicep.EvidenceDates).Should().Equal([new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 20)]);
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

    await FluentActions.Awaiting(() => service.SummarizeTrainingAsync(4, null, 1, evidence.Continuation, default)).Should().ThrowExactlyAsync<ArgumentException>();
    (client.CallCount).Should().Be(callsBefore);
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

    (second.RangeStartUtc).Should().Be(first.RangeStartUtc);
    (second.RangeEndUtc).Should().Be(first.RangeEndUtc);
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

    (result.WeeklyFrequency.Sum(static week => week.ChunkWorkoutCount)).Should().Be(2);
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
        (chunk.MetricScope).Should().Be("partial_chunk");
        (chunk.ContinuationInputs).Should().NotBeNull();
        (chunk.ContinuationInputs.Weeks).Should().Be(4);
        (chunk.ContinuationInputs.RangeEndUtc).Should().Be(end);
        (chunk.ContinuationInputs.Limit).Should().Be(2);
      }
      continuation = chunk.Continuation;
    }
    while (continuation is not null);

    (complete.MetricScope).Should().Be("complete_period");
    (complete.GapsComplete).Should().BeTrue();
    (chunks.Sum(static chunk => chunk.ChunkWorkoutFrequency)).Should().Be(complete.ChunkWorkoutFrequency);
    foreach (var fullWeek in complete.WeeklyFrequency)
    {
      (chunks.Sum(chunk => chunk.WeeklyFrequency.Single(week => week.PeriodStartUtc == fullWeek.PeriodStartUtc).ChunkWorkoutCount)).Should().Be(fullWeek.ChunkWorkoutCount);
    }
    var fullExercise = (complete.Exercises).Should().ContainSingle().Which;
    (chunks.SelectMany(static chunk => chunk.Exercises).Sum(static exercise => exercise.ChunkVolumeKgReps)).Should().Be(fullExercise.ChunkVolumeKgReps);
    var observations = chunks.SelectMany(static chunk => chunk.Exercises)
        .SelectMany(static exercise => new[] { exercise.FirstObservation, exercise.LastObservation })
        .OrderBy(static observation => observation.StartTime).ToArray();
    (observations[^1].VolumeKgReps - observations[0].VolumeKgReps).Should().Be(fullExercise.ChunkProgressionKgReps);
    var composedGapStarts = complete.WeeklyFrequency
        .Where(fullWeek => chunks.Sum(chunk => chunk.WeeklyFrequency.Single(week => week.PeriodStartUtc == fullWeek.PeriodStartUtc).ChunkWorkoutCount) == 0)
        .Select(static week => week.PeriodStartUtc).ToArray();
    (composedGapStarts).Should().Equal(complete.MissingWeekGaps.Select(static gap => gap.PeriodStartUtc));
    (chunks.Where(static chunk => !chunk.GapsComplete)).Should().AllSatisfy(static chunk => (chunk.MissingWeekGaps).Should().BeEmpty());
    var weightSamples = chunks.SelectMany(static chunk => chunk.MeasurementDeltas)
        .Where(static delta => delta.Metric == "weight_kg")
        .SelectMany(static delta => new[] { (Date: delta.EvidenceDates[0], Value: delta.FirstValue), (Date: delta.EvidenceDates[1], Value: delta.LastValue) })
        .OrderBy(static sample => sample.Date).ToArray();
    var fullWeight = (complete.MeasurementDeltas).Should().ContainSingle(static delta => delta.Metric == "weight_kg").Which;
    (weightSamples[^1].Value - weightSamples[0].Value).Should().Be(fullWeight.Delta);
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

    (first.Truncated).Should().BeTrue();
    (first.MetricScope).Should().Be("partial_chunk");
    (first.ContinuationInputs).Should().NotBeNull();
    (second.Truncated).Should().BeFalse();
    (second.MetricScope).Should().Be("partial_chunk");
    (client.CallCount).Should().Be(102);
  }

  private static Workout Workout(string id, string start, decimal weight) => FakeHevyClient.SampleWorkout() with
  {
    Id = id,
    StartTime = DateTimeOffset.Parse(start),
    EndTime = DateTimeOffset.Parse(start).AddHours(1),
    Exercises = [new WorkoutExercise(0, "Squat", "", "template-1", null, [new WorkoutSet(0, "normal", weight, 5, null, null, null, null)])],
  };

  private static ExerciseHistoryEntry History(string workoutId, string start, decimal weight, int reps) =>
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
