using System.Globalization;

namespace Hevy.Core.UseCases;

public sealed class TrainingAnalysisUseCase(IHevyClient client, TimeProvider timeProvider)
{
  private const string EvidenceEndpoint = "workout-evidence";
  private const string TrainingEndpoint = "training-summary";
  private const string HistoryEndpoint = "exercise-history-summary";
  private const string WorkoutsPhase = "workouts";
  private const string MeasurementsPhase = "measurements";
  private const string HistoryPhase = "history";

  public async Task<WorkoutEvidenceResult> GetWorkoutEvidenceAsync(
      int? weeks,
      DateTimeOffset? rangeEndUtc,
      int limit,
      string? continuation,
      CancellationToken cancellationToken)
  {
    var cursor = ResolveCursor(EvidenceEndpoint, WorkoutsPhase, weeks, rangeEndUtc, limit, continuation, [WorkoutsPhase]);
    var fetch = await FetchWorkoutChunkAsync(cursor, limit, Continuation.MaximumItemBudget, cancellationToken).ConfigureAwait(false);
    var next = fetch.More ? Next(cursor, WorkoutsPhase, fetch.NextPage) : null;
    return new WorkoutEvidenceResult(
        fetch.Items.Select(ProjectWorkout).ToImmutableList(),
        cursor.Range.Weeks,
        cursor.Range.Start,
        cursor.Range.End,
        next is not null,
        next,
        Inputs(cursor, next));
  }

  public async Task<TrainingSummary> SummarizeTrainingAsync(
      int? weeks,
      DateTimeOffset? rangeEndUtc,
      int limit,
      string? continuation,
      CancellationToken cancellationToken)
  {
    var cursor = ResolveCursor(TrainingEndpoint, WorkoutsPhase, weeks, rangeEndUtc, limit, continuation, [WorkoutsPhase, MeasurementsPhase]);
    var capacity = limit;
    var scanBudget = Continuation.MaximumItemBudget;
    var workouts = new List<Workout>();
    var measurements = new List<BodyMeasurement>();
    var workoutPhaseCompleteInThisCall = false;
    TrainingSummary Build(string? next) => BuildTrainingSummary(
        cursor,
        workouts,
        measurements,
        cursor.IsInitial && next is null,
        workoutPhaseCompleteInThisCall,
        next);

    if (cursor.Phase == MeasurementsPhase)
    {
      var measurementFetch = await FetchMeasurementChunkAsync(cursor, capacity, scanBudget, cancellationToken).ConfigureAwait(false);
      measurements.AddRange(measurementFetch.Items);
      return Build(measurementFetch.More ? Next(cursor, MeasurementsPhase, measurementFetch.NextPage) : null);
    }

    var workoutFetch = await FetchWorkoutChunkAsync(cursor, capacity, scanBudget, cancellationToken).ConfigureAwait(false);
    workouts.AddRange(workoutFetch.Items);
    if (workoutFetch.More)
    {
      return Build(Next(cursor, WorkoutsPhase, workoutFetch.NextPage));
    }

    workoutPhaseCompleteInThisCall = cursor.IsInitial;
    capacity -= workoutFetch.Items.Count;
    scanBudget -= workoutFetch.ScannedCapacity;
    if (capacity < cursor.PageSize || scanBudget < cursor.PageSize)
    {
      return Build(Next(cursor, MeasurementsPhase, 1));
    }

    var remainingMeasurements = await FetchMeasurementChunkAsync(
        cursor with { Phase = MeasurementsPhase, NextPage = 1 },
        capacity,
        scanBudget,
        cancellationToken).ConfigureAwait(false);
    measurements.AddRange(remainingMeasurements.Items);
    return Build(remainingMeasurements.More ? Next(cursor, MeasurementsPhase, remainingMeasurements.NextPage) : null);
  }

  public async Task<ExerciseHistorySummary> SummarizeExerciseHistoryAsync(
      string exerciseTemplateId,
      int? weeks,
      DateTimeOffset? rangeEndUtc,
      int limit,
      string? continuation,
      CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(exerciseTemplateId)) throw new ArgumentException("An exercise template identifier is required.", nameof(exerciseTemplateId));
    var cursor = ResolveCursor(
        HistoryEndpoint,
        HistoryPhase,
        weeks,
        rangeEndUtc,
        limit,
        continuation,
        [HistoryPhase],
        exerciseTemplateId,
        Continuation.MaximumItemBudget);
    var offset = ExerciseHistoryQuery.PageOffset(cursor.NextPage, cursor.PageSize);
    var startDate = DateOnly.FromDateTime(cursor.Range.Start.UtcDateTime);
    var endDate = DateOnly.FromDateTime(cursor.Range.End.AddTicks(-1).UtcDateTime);
    var result = await client.GetExerciseHistoryWindowAsync(
        exerciseTemplateId,
        new ExerciseHistoryQuery(offset, cursor.PageSize, startDate, endDate, cursor.Range.Start, cursor.Range.End),
        cancellationToken).ConfigureAwait(false);
    var entries = result.Items.OrderBy(static entry => entry.WorkoutStartTime)
        .ThenBy(static entry => entry.WorkoutId, StringComparer.Ordinal).ToArray();
    var observations = entries.GroupBy(static entry => entry.WorkoutId, StringComparer.Ordinal)
        .Select(group =>
        {
          var first = group.OrderBy(static entry => entry.WorkoutStartTime).First();
          var values = group.Select(EntryVolume).Where(static value => value is not null).Select(static value => value!.Value).ToArray();
          return new ExerciseVolumeObservation(group.Key, first.WorkoutStartTime, values.Sum());
        })
        .OrderBy(static observation => observation.StartTime).ThenBy(static observation => observation.WorkoutId, StringComparer.Ordinal).ToArray();
    var more = result.Truncated;
    var next = more && result.TruncationReason is null ? Next(cursor, HistoryPhase, cursor.NextPage + 1) : null;
    var firstObservation = observations.FirstOrDefault();
    var lastObservation = observations.LastOrDefault();
    return new ExerciseHistorySummary(
        cursor.IsInitial && !more ? "complete_period" : "partial_chunk",
        exerciseTemplateId,
        cursor.Range.Weeks,
        cursor.Range.Start,
        cursor.Range.End,
        entries.Length,
        result.ScannedItemCount,
        observations.Sum(static observation => observation.VolumeKgReps),
        observations.Length < 2 ? null : observations[^1].VolumeKgReps - observations[0].VolumeKgReps,
        firstObservation,
        lastObservation,
        observations.Select(static observation => new ExerciseHistoryEvidence(observation.WorkoutId, observation.StartTime, observation.VolumeKgReps)).ToImmutableList(),
        more,
        result.TruncationReason,
        next,
        Inputs(cursor, next));
  }

  private TrainingSummary BuildTrainingSummary(
      AnalysisCursor cursor,
      IReadOnlyList<Workout> workoutChunk,
      IReadOnlyList<BodyMeasurement> measurementChunk,
      bool completePeriod,
      bool gapsComplete,
      string? continuation)
  {
    var workouts = workoutChunk.OrderBy(static workout => workout.StartTime).ThenBy(static workout => workout.Id, StringComparer.Ordinal).ToArray();
    var weeksResult = Enumerable.Range(0, cursor.Range.Weeks)
        .Select(offset => cursor.Range.Start.AddDays(offset * 7))
        .Select(periodStart =>
        {
          var periodEnd = periodStart.AddDays(7);
          var evidence = workouts.Where(workout => workout.StartTime >= periodStart && workout.StartTime < periodEnd)
              .Select(static workout => new WorkoutEvidenceReference(workout.Id, workout.StartTime))
              .DistinctBy(static item => item.WorkoutId, StringComparer.Ordinal).ToImmutableList();
          return new WeeklyFrequency(periodStart, periodEnd, evidence.Count, evidence);
        })
        .ToImmutableList();
    var exercises = workouts.SelectMany(workout => workout.Exercises.Select(exercise => new
    {
      Workout = workout,
      Exercise = exercise,
      Volume = Volume(exercise.Sets),
    }))
        .GroupBy(static item => item.Exercise.ExerciseTemplateId, StringComparer.Ordinal)
        .Select(group =>
        {
          var ordered = group.OrderBy(static item => item.Workout.StartTime).ThenBy(static item => item.Workout.Id, StringComparer.Ordinal).ToArray();
          var observations = ordered.GroupBy(static item => item.Workout.Id, StringComparer.Ordinal)
              .Select(static workout => new ExerciseVolumeObservation(
                  workout.Key,
                  workout.First().Workout.StartTime,
                  workout.Sum(static item => item.Volume)))
              .OrderBy(static item => item.StartTime)
              .ThenBy(static item => item.WorkoutId, StringComparer.Ordinal)
              .ToArray();
          var evidence = observations.Select(static item => new WorkoutEvidenceReference(item.WorkoutId, item.StartTime)).ToImmutableList();
          return new ExerciseTrainingSummary(
              group.Key,
              ordered[^1].Exercise.Title,
              observations.Sum(static item => item.VolumeKgReps),
              observations.Length < 2 ? null : observations[^1].VolumeKgReps - observations[0].VolumeKgReps,
              observations[0],
              observations[^1],
              evidence);
        })
        .OrderBy(static item => item.Title, StringComparer.OrdinalIgnoreCase)
        .ThenBy(static item => item.ExerciseTemplateId, StringComparer.Ordinal).ToImmutableList();
    var gaps = gapsComplete
        ? weeksResult.Where(static week => week.ChunkWorkoutCount == 0)
            .Select(static week => new MissingWeekGap(week.PeriodStartUtc, week.PeriodEndUtc)).ToImmutableList()
        : [];
    return new TrainingSummary(
        completePeriod ? "complete_period" : "partial_chunk",
        cursor.Range.Weeks,
        cursor.Range.Start,
        cursor.Range.End,
        workouts.Length,
        weeksResult,
        exercises,
        gapsComplete,
        gaps,
        MeasurementDeltas(measurementChunk),
        workouts.Select(static workout => new WorkoutEvidenceReference(workout.Id, workout.StartTime))
            .DistinctBy(static item => item.WorkoutId, StringComparer.Ordinal).ToImmutableList(),
        continuation is not null,
        continuation,
        Inputs(cursor, continuation));
  }

  private async Task<PageChunk<Workout>> FetchWorkoutChunkAsync(
      AnalysisCursor cursor,
      int capacity,
      int scanBudget,
      CancellationToken cancellationToken)
  {
    var items = new List<Workout>();
    var page = cursor.NextPage;
    var pageCount = page;
    var scanned = 0;
    while (page <= pageCount && scanBudget - scanned >= cursor.PageSize && capacity - items.Count >= cursor.PageSize)
    {
      var result = await client.GetWorkoutsAsync(page, cursor.PageSize, cancellationToken).ConfigureAwait(false);
      ValidatePage(result.Page, result.PageCount, page);
      pageCount = result.PageCount;
      scanned += cursor.PageSize;
      items.AddRange(result.Items.Where(workout => workout.StartTime >= cursor.Range.Start && workout.StartTime < cursor.Range.End));
      page++;
    }
    return new PageChunk<Workout>(items, page <= pageCount, page, scanned);
  }

  private async Task<PageChunk<BodyMeasurement>> FetchMeasurementChunkAsync(
      AnalysisCursor cursor,
      int capacity,
      int scanBudget,
      CancellationToken cancellationToken)
  {
    var items = new List<BodyMeasurement>();
    var page = cursor.NextPage;
    var pageCount = page;
    var scanned = 0;
    while (page <= pageCount && scanBudget - scanned >= cursor.PageSize && capacity - items.Count >= cursor.PageSize)
    {
      var result = await client.GetBodyMeasurementsAsync(page, cursor.PageSize, cancellationToken).ConfigureAwait(false);
      ValidatePage(result.Page, result.PageCount, page);
      pageCount = result.PageCount;
      scanned += cursor.PageSize;
      items.AddRange(result.Items.Where(measurement =>
      {
        var instant = new DateTimeOffset(measurement.Date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        return instant >= cursor.Range.Start && instant < cursor.Range.End;
      }));
      page++;
    }
    return new PageChunk<BodyMeasurement>(items, page <= pageCount, page, scanned);
  }

  private AnalysisCursor ResolveCursor(
      string endpoint,
      string initialPhase,
      int? weeks,
      DateTimeOffset? rangeEndUtc,
      int limit,
      string? continuation,
      ImmutableList<string> allowedPhases,
      string? exerciseTemplateId = null,
      int maximumPageSize = 10)
  {
    ValidateLimit(limit);
    var selectedWeeks = weeks ?? 4;
    ArgumentOutOfRangeException.ThrowIfLessThan(selectedWeeks, 1);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(selectedWeeks, 52);
    var pageSize = Math.Min(maximumPageSize, limit);
    if (continuation is null)
    {
      var initialRange = ResolveRange(selectedWeeks, rangeEndUtc);
      var filters = CursorFilters(initialRange, limit, pageSize, initialPhase, exerciseTemplateId);
      return new AnalysisCursor(endpoint, initialPhase, 1, initialRange, limit, pageSize, filters, true);
    }

    var state = Continuation.Parse(continuation, endpoint);
    if (state.RemainingItemBudget != Continuation.MaximumItemBudget)
    {
      throw new ArgumentException("The continuation has an invalid per-invocation scan budget.", nameof(continuation));
    }
    var phase = RequiredFilter(state.Filters, "phase");
    if (!allowedPhases.Contains(phase)) throw new ArgumentException("The continuation phase is invalid for this operation.", nameof(continuation));
    var tokenWeeks = ParseIntFilter(state.Filters, "weeks");
    var tokenLimit = ParseIntFilter(state.Filters, "limit");
    var tokenPageSize = ParseIntFilter(state.Filters, "page_size");
    var tokenEnd = ParseInstantFilter(state.Filters, "end_utc");
    var tokenStart = ParseInstantFilter(state.Filters, "start_utc");
    var matchesReplayInputs = selectedWeeks == tokenWeeks &&
        limit == tokenLimit &&
        pageSize == tokenPageSize &&
        (rangeEndUtc is null || rangeEndUtc.Value.ToUniversalTime() == tokenEnd);
    if (!matchesReplayInputs)
    {
      throw new ArgumentException("The continuation does not match the stable replay inputs.", nameof(continuation));
    }
    var range = new UtcRange(tokenWeeks, tokenStart, tokenEnd);
    if (range.Start != range.End.AddDays(-7 * range.Weeks)) throw new ArgumentException("The continuation range is inconsistent.", nameof(continuation));
    var expected = CursorFilters(range, limit, pageSize, phase, exerciseTemplateId);
    Continuation.Parse(continuation, endpoint, expected);
    return new AnalysisCursor(endpoint, phase, state.NextPage, range, limit, pageSize, expected, false);
  }

  private UtcRange ResolveRange(int weeks, DateTimeOffset? end)
  {
    var rangeEnd = end?.ToUniversalTime() ?? NextUtcMondayBoundary(timeProvider.GetUtcNow());
    return new UtcRange(weeks, rangeEnd.AddDays(-7 * weeks), rangeEnd);
  }

  private static IReadOnlyDictionary<string, string?> CursorFilters(
      UtcRange range,
      int limit,
      int pageSize,
      string phase,
      string? exerciseTemplateId) => Filters(
          ("end_utc", Format(range.End)),
          ("exercise_template_id", exerciseTemplateId),
          ("limit", limit.ToString(CultureInfo.InvariantCulture)),
          ("page_size", pageSize.ToString(CultureInfo.InvariantCulture)),
          ("phase", phase),
          ("start_utc", Format(range.Start)),
          ("weeks", range.Weeks.ToString(CultureInfo.InvariantCulture)));

  private static string Next(AnalysisCursor cursor, string phase, int page) =>
      Continuation.Create(cursor.Endpoint, page, CursorFilters(cursor.Range, cursor.Limit, cursor.PageSize, phase, cursor.Filters["exercise_template_id"]), Continuation.MaximumItemBudget);

  private static CompositeContinuationInputs? Inputs(AnalysisCursor cursor, string? continuation) => continuation is null
      ? null
      : new CompositeContinuationInputs(cursor.Range.Weeks, cursor.Range.End, cursor.Limit, continuation);

  private static DateTimeOffset NextUtcMondayBoundary(DateTimeOffset now)
  {
    var utc = now.ToUniversalTime();
    var date = DateOnly.FromDateTime(utc.UtcDateTime);
    var days = ((int)DayOfWeek.Monday - (int)date.DayOfWeek + 7) % 7;
    if (days == 0 && utc.TimeOfDay != TimeSpan.Zero) days = 7;
    return new DateTimeOffset(date.AddDays(days).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
  }

  private static WorkoutEvidenceItem ProjectWorkout(Workout workout) => new(
      workout.Id,
      workout.Title,
      workout.StartTime,
      workout.EndTime,
      workout.Exercises.Select(exercise => new ExerciseEvidenceItem(
          exercise.ExerciseTemplateId,
          exercise.Title,
          Volume(exercise.Sets),
          exercise.Sets.Count(static set => set.WeightKg is not null && set.Reps is not null))).ToImmutableList());

  private static decimal Volume(IEnumerable<WorkoutSet> sets) =>
      sets.Where(static set => set.WeightKg is not null && set.Reps is not null)
          .Sum(static set => set.WeightKg!.Value * set.Reps!.Value);

  private static decimal? EntryVolume(ExerciseHistoryEntry entry) =>
      entry.WeightKg is not null && entry.Reps is not null ? entry.WeightKg.Value * entry.Reps.Value : null;

  private static ImmutableList<MeasurementDelta> MeasurementDeltas(IReadOnlyList<BodyMeasurement> measurements)
  {
    var ordered = measurements.OrderBy(static measurement => measurement.Date).ToArray();
    var deltas = new List<MeasurementDelta>();
    Add("weight_kg", static measurement => measurement.WeightKg);
    Add("lean_mass_kg", static measurement => measurement.LeanMassKg);
    Add("fat_percent", static measurement => measurement.FatPercent);
    Add("neck_cm", static measurement => measurement.NeckCm);
    Add("shoulder_cm", static measurement => measurement.ShoulderCm);
    Add("chest_cm", static measurement => measurement.ChestCm);
    Add("left_bicep_cm", static measurement => measurement.LeftBicepCm);
    Add("right_bicep_cm", static measurement => measurement.RightBicepCm);
    Add("left_forearm_cm", static measurement => measurement.LeftForearmCm);
    Add("right_forearm_cm", static measurement => measurement.RightForearmCm);
    Add("abdomen", static measurement => measurement.Abdomen);
    Add("waist", static measurement => measurement.Waist);
    Add("hips", static measurement => measurement.Hips);
    Add("left_thigh", static measurement => measurement.LeftThigh);
    Add("right_thigh", static measurement => measurement.RightThigh);
    Add("left_calf", static measurement => measurement.LeftCalf);
    Add("right_calf", static measurement => measurement.RightCalf);
    return deltas.ToImmutableList();

    void Add(string metric, Func<BodyMeasurement, decimal?> selector)
    {
      var samples = ordered.Select(measurement => (measurement.Date, Value: selector(measurement)))
          .Where(static sample => sample.Value is not null).ToArray();
      if (samples.Length == 0) return;
      var first = samples[0];
      var last = samples[^1];
      deltas.Add(new MeasurementDelta(metric, first.Value!.Value, last.Value!.Value, last.Value.Value - first.Value.Value, [first.Date, last.Date]));
    }
  }

  private static IReadOnlyDictionary<string, string?> Filters(params (string Key, string? Value)[] filters) =>
      new SortedDictionary<string, string?>(filters.ToDictionary(static filter => filter.Key, static filter => filter.Value, StringComparer.Ordinal), StringComparer.Ordinal);

  private static string RequiredFilter(IReadOnlyDictionary<string, string?> filters, string name) =>
      filters.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
          ? value
          : throw new ArgumentException($"The continuation omits {name}.", nameof(filters));

  private static int ParseIntFilter(IReadOnlyDictionary<string, string?> filters, string name) =>
      int.TryParse(RequiredFilter(filters, name), NumberStyles.None, CultureInfo.InvariantCulture, out var value)
          ? value
          : throw new ArgumentException($"The continuation has an invalid {name}.", nameof(filters));

  private static DateTimeOffset ParseInstantFilter(IReadOnlyDictionary<string, string?> filters, string name) =>
      DateTimeOffset.TryParseExact(RequiredFilter(filters, name), "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value)
          ? value.ToUniversalTime()
          : throw new ArgumentException($"The continuation has an invalid {name}.", nameof(filters));

  private static string Format(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

  private static void ValidateLimit(int limit)
  {
    ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, Continuation.MaximumItemBudget);
  }

  private static void ValidatePage(int actualPage, int pageCount, int expectedPage)
  {
    if (actualPage != expectedPage || pageCount < 0)
    {
      throw new InvalidOperationException("Hevy returned inconsistent pagination for the bounded analysis.");
    }
  }

}
