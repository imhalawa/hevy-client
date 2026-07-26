using System.Globalization;
using Hevy.Client;
using Hevy.Client.Models;

namespace Hevy.Mcp.Composite;

internal sealed record WorkoutEvidenceResult(
    IReadOnlyList<WorkoutEvidenceItem> Items,
    int Weeks,
    DateTimeOffset RangeStartUtc,
    DateTimeOffset RangeEndUtc,
    bool Truncated,
    string? Continuation);

internal sealed record WorkoutEvidenceItem(
    string WorkoutId,
    string Title,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    IReadOnlyList<ExerciseEvidenceItem> Exercises);

internal sealed record ExerciseEvidenceItem(
    string ExerciseTemplateId,
    string Title,
    decimal VolumeKgReps,
    int CountedSets);

internal sealed record WorkoutEvidenceReference(string WorkoutId, DateTimeOffset StartTime);

internal sealed record WeeklyFrequency(DateOnly WeekStartUtc, int WorkoutCount, IReadOnlyList<WorkoutEvidenceReference> Evidence);

internal sealed record ExerciseTrainingSummary(
    string ExerciseTemplateId,
    string Title,
    decimal VolumeKgReps,
    decimal? ProgressionKgReps,
    IReadOnlyList<WorkoutEvidenceReference> Evidence);

internal sealed record MissingWeekGap(DateOnly WeekStartUtc, DateOnly WeekEndUtc);

internal sealed record MeasurementDelta(
    string Metric,
    decimal FirstValue,
    decimal LastValue,
    decimal Delta,
    IReadOnlyList<DateOnly> EvidenceDates);

internal sealed record TrainingSummary(
    int Weeks,
    DateTimeOffset RangeStartUtc,
    DateTimeOffset RangeEndUtc,
    int WorkoutFrequency,
    IReadOnlyList<WeeklyFrequency> WeeklyFrequency,
    IReadOnlyList<ExerciseTrainingSummary> Exercises,
    IReadOnlyList<MissingWeekGap> MissingWeekGaps,
    IReadOnlyList<MeasurementDelta> MeasurementDeltas,
    IReadOnlyList<WorkoutEvidenceReference> Evidence,
    bool Truncated,
    string? Continuation);

internal sealed record ExerciseHistoryEvidence(
    string WorkoutId,
    DateTimeOffset WorkoutStartTime,
    decimal? VolumeKgReps);

internal sealed record ExerciseHistorySummary(
    string ExerciseTemplateId,
    int Weeks,
    DateTimeOffset RangeStartUtc,
    DateTimeOffset RangeEndUtc,
    int EntryCount,
    decimal VolumeKgReps,
    decimal? ProgressionKgReps,
    IReadOnlyList<ExerciseHistoryEvidence> Evidence,
    bool Truncated,
    string? Continuation);

internal sealed class TrainingAnalysisService(IHevyClient client, TimeProvider timeProvider)
{
  internal async Task<WorkoutEvidenceResult> GetWorkoutEvidenceAsync(
      int? weeks,
      DateTimeOffset? rangeEndUtc,
      int limit,
      string? continuation,
      CancellationToken cancellationToken)
  {
    var range = ResolveRange(weeks, rangeEndUtc);
    var fetch = await FetchWorkoutsAsync(range, limit, continuation, cancellationToken).ConfigureAwait(false);
    return new WorkoutEvidenceResult(
        fetch.Workouts.Select(ProjectWorkout).ToArray(),
        range.Weeks,
        range.Start,
        range.End,
        fetch.Truncated,
        fetch.Continuation);
  }

  internal async Task<TrainingSummary> SummarizeTrainingAsync(
      int? weeks,
      DateTimeOffset? rangeEndUtc,
      int limit,
      string? continuation,
      CancellationToken cancellationToken)
  {
    var range = ResolveRange(weeks, rangeEndUtc);
    var fetch = await FetchWorkoutsAsync(range, limit, continuation, cancellationToken).ConfigureAwait(false);
    var workouts = fetch.Workouts.OrderBy(static workout => workout.StartTime).ThenBy(static workout => workout.Id, StringComparer.Ordinal).ToArray();
    var weeksResult = Enumerable.Range(0, range.Weeks)
        .Select(offset => DateOnly.FromDateTime(range.Start.UtcDateTime).AddDays(offset * 7))
        .Select(weekStart =>
        {
          var weekEnd = weekStart.AddDays(7);
          var evidence = workouts.Where(workout => DateOnly.FromDateTime(workout.StartTime.UtcDateTime) >= weekStart && DateOnly.FromDateTime(workout.StartTime.UtcDateTime) < weekEnd)
              .Select(static workout => new WorkoutEvidenceReference(workout.Id, workout.StartTime))
              .DistinctBy(static item => item.WorkoutId, StringComparer.Ordinal).ToArray();
          return new WeeklyFrequency(weekStart, evidence.Length, evidence);
        })
        .ToArray();

    var exercises = workouts
        .SelectMany(workout => workout.Exercises.Select(exercise => new
        {
          Workout = workout,
          Exercise = exercise,
          Volume = Volume(exercise.Sets),
        }))
        .GroupBy(static item => item.Exercise.ExerciseTemplateId, StringComparer.Ordinal)
        .Select(group =>
        {
          var ordered = group.OrderBy(static item => item.Workout.StartTime).ThenBy(static item => item.Workout.Id, StringComparer.Ordinal).ToArray();
          var evidence = ordered.Select(static item => new WorkoutEvidenceReference(item.Workout.Id, item.Workout.StartTime))
              .DistinctBy(static item => item.WorkoutId, StringComparer.Ordinal).ToArray();
          decimal? progression = ordered.Length < 2 ? null : ordered[^1].Volume - ordered[0].Volume;
          return new ExerciseTrainingSummary(group.Key, ordered[^1].Exercise.Title, ordered.Sum(static item => item.Volume), progression, evidence);
        })
        .OrderBy(static item => item.Title, StringComparer.OrdinalIgnoreCase)
        .ThenBy(static item => item.ExerciseTemplateId, StringComparer.Ordinal)
        .ToArray();
    var gaps = weeksResult.Where(static week => week.WorkoutCount == 0)
        .Select(static week => new MissingWeekGap(week.WeekStartUtc, week.WeekStartUtc.AddDays(6)))
        .ToArray();
    var measurements = await FetchMeasurementsAsync(range, cancellationToken).ConfigureAwait(false);

    return new TrainingSummary(
        range.Weeks,
        range.Start,
        range.End,
        workouts.Length,
        weeksResult,
        exercises,
        gaps,
        MeasurementDeltas(measurements),
        workouts.Select(static workout => new WorkoutEvidenceReference(workout.Id, workout.StartTime))
            .DistinctBy(static item => item.WorkoutId, StringComparer.Ordinal).ToArray(),
        fetch.Truncated,
        fetch.Continuation);
  }

  internal async Task<ExerciseHistorySummary> SummarizeExerciseHistoryAsync(
      string exerciseTemplateId,
      int? weeks,
      DateTimeOffset? rangeEndUtc,
      int limit,
      string? continuation,
      CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(exerciseTemplateId)) throw new ArgumentException("An exercise template identifier is required.", nameof(exerciseTemplateId));
    ValidateLimit(limit);
    var range = ResolveRange(weeks, rangeEndUtc);
    var pageSize = Math.Min(10, limit);
    var filters = Filters(
        ("end_utc", Format(range.End)),
        ("exercise_template_id", exerciseTemplateId),
        ("page_size", pageSize.ToString(CultureInfo.InvariantCulture)),
        ("start_utc", Format(range.Start)),
        ("weeks", range.Weeks.ToString(CultureInfo.InvariantCulture)));
    var state = continuation is null
        ? new ContinuationState("exercise-history-summary", 1, filters, Continuation.MaximumItemBudget)
        : Continuation.Parse(continuation, "exercise-history-summary", filters);
    var entries = new List<ExerciseHistoryEntry>();
    var page = state.NextPage;
    var pageCount = page;
    var budget = state.RemainingItemBudget;
    var startDate = DateOnly.FromDateTime(range.Start.UtcDateTime);
    var endDate = DateOnly.FromDateTime(range.End.AddTicks(-1).UtcDateTime);
    while (page <= pageCount && budget > 0 && entries.Count + pageSize <= limit)
    {
      var result = await client.GetExerciseHistoryAsync(exerciseTemplateId, page, pageSize, startDate, endDate, cancellationToken).ConfigureAwait(false);
      ValidatePage(result.Page, result.PageCount, page);
      pageCount = result.PageCount;
      budget -= pageSize;
      entries.AddRange(result.Items.Where(entry => entry.WorkoutStartTime >= range.Start && entry.WorkoutStartTime < range.End));
      page++;
    }
    var more = page <= pageCount;
    var next = more ? Continuation.Create("exercise-history-summary", page, filters, budget > 0 ? budget : Continuation.MaximumItemBudget) : null;
    var ordered = entries.OrderBy(static entry => entry.WorkoutStartTime).ThenBy(static entry => entry.WorkoutId, StringComparer.Ordinal).ToArray();
    var evidence = ordered.Select(static entry => new ExerciseHistoryEvidence(entry.WorkoutId, entry.WorkoutStartTime, EntryVolume(entry))).ToArray();
    var volumeValues = evidence.Where(static item => item.VolumeKgReps is not null).Select(static item => item.VolumeKgReps!.Value).ToArray();
    decimal? progression = volumeValues.Length < 2 ? null : volumeValues[^1] - volumeValues[0];
    return new ExerciseHistorySummary(
        exerciseTemplateId,
        range.Weeks,
        range.Start,
        range.End,
        ordered.Length,
        volumeValues.Sum(),
        progression,
        evidence,
        more,
        next);
  }

  private async Task<WorkoutFetch> FetchWorkoutsAsync(
      UtcRange range,
      int limit,
      string? continuation,
      CancellationToken cancellationToken)
  {
    ValidateLimit(limit);
    var pageSize = Math.Min(10, limit);
    var filters = Filters(
        ("end_utc", Format(range.End)),
        ("page_size", pageSize.ToString(CultureInfo.InvariantCulture)),
        ("start_utc", Format(range.Start)),
        ("weeks", range.Weeks.ToString(CultureInfo.InvariantCulture)));
    var state = continuation is null
        ? new ContinuationState("workout-evidence", 1, filters, Continuation.MaximumItemBudget)
        : Continuation.Parse(continuation, "workout-evidence", filters);
    var workouts = new List<Workout>();
    var page = state.NextPage;
    var pageCount = page;
    var budget = state.RemainingItemBudget;
    while (page <= pageCount && budget > 0 && workouts.Count + pageSize <= limit)
    {
      var result = await client.GetWorkoutsAsync(page, pageSize, cancellationToken).ConfigureAwait(false);
      ValidatePage(result.Page, result.PageCount, page);
      pageCount = result.PageCount;
      budget -= pageSize;
      workouts.AddRange(result.Items.Where(workout => workout.StartTime >= range.Start && workout.StartTime < range.End));
      page++;
    }
    var more = page <= pageCount;
    var next = more ? Continuation.Create("workout-evidence", page, filters, budget > 0 ? budget : Continuation.MaximumItemBudget) : null;
    return new WorkoutFetch(workouts, more, next);
  }

  private async Task<IReadOnlyList<BodyMeasurement>> FetchMeasurementsAsync(UtcRange range, CancellationToken cancellationToken)
  {
    var measurements = new List<BodyMeasurement>();
    for (var page = 1; page <= 100; page++)
    {
      var result = await client.GetBodyMeasurementsAsync(page, 10, cancellationToken).ConfigureAwait(false);
      ValidatePage(result.Page, result.PageCount, page);
      measurements.AddRange(result.Items.Where(measurement =>
      {
        var instant = new DateTimeOffset(measurement.Date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        return instant >= range.Start && instant < range.End;
      }));
      if (page >= result.PageCount) return measurements.OrderBy(static measurement => measurement.Date).ToArray();
    }
    throw new InvalidOperationException("Body measurements exceed the bounded 1,000-item analysis limit.");
  }

  private UtcRange ResolveRange(int? weeks, DateTimeOffset? end)
  {
    var selectedWeeks = weeks ?? 4;
    ArgumentOutOfRangeException.ThrowIfLessThan(selectedWeeks, 1);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(selectedWeeks, 52);
    var rangeEnd = end?.ToUniversalTime() ?? NextUtcMondayBoundary(timeProvider.GetUtcNow());
    return new UtcRange(selectedWeeks, rangeEnd.AddDays(-7 * selectedWeeks), rangeEnd);
  }

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
      workout.Exercises.Select(exercise =>
      {
        var counted = exercise.Sets.Count(static set => set.WeightKg is not null && set.Reps is not null);
        return new ExerciseEvidenceItem(exercise.ExerciseTemplateId, exercise.Title, Volume(exercise.Sets), counted);
      }).ToArray());

  private static decimal Volume(IEnumerable<WorkoutSet> sets) =>
      sets.Where(static set => set.WeightKg is not null && set.Reps is not null)
          .Sum(static set => set.WeightKg!.Value * set.Reps!.Value);

  private static decimal? EntryVolume(ExerciseHistoryEntry entry) =>
      entry.WeightKg is not null && entry.Reps is not null ? entry.WeightKg.Value * entry.Reps.Value : null;

  private static IReadOnlyList<MeasurementDelta> MeasurementDeltas(IReadOnlyList<BodyMeasurement> measurements)
  {
    if (measurements.Count < 2) return [];
    var first = measurements[0];
    var last = measurements[^1];
    var dates = new[] { first.Date, last.Date };
    var deltas = new List<MeasurementDelta>();
    Add("weight_kg", first.WeightKg, last.WeightKg);
    Add("lean_mass_kg", first.LeanMassKg, last.LeanMassKg);
    Add("fat_percent", first.FatPercent, last.FatPercent);
    Add("neck_cm", first.NeckCm, last.NeckCm);
    Add("shoulder_cm", first.ShoulderCm, last.ShoulderCm);
    Add("chest_cm", first.ChestCm, last.ChestCm);
    Add("left_bicep_cm", first.LeftBicepCm, last.LeftBicepCm);
    Add("right_bicep_cm", first.RightBicepCm, last.RightBicepCm);
    Add("left_forearm_cm", first.LeftForearmCm, last.LeftForearmCm);
    Add("right_forearm_cm", first.RightForearmCm, last.RightForearmCm);
    Add("abdomen", first.Abdomen, last.Abdomen);
    Add("waist", first.Waist, last.Waist);
    Add("hips", first.Hips, last.Hips);
    Add("left_thigh", first.LeftThigh, last.LeftThigh);
    Add("right_thigh", first.RightThigh, last.RightThigh);
    Add("left_calf", first.LeftCalf, last.LeftCalf);
    Add("right_calf", first.RightCalf, last.RightCalf);
    return deltas;

    void Add(string metric, decimal? firstValue, decimal? lastValue)
    {
      if (firstValue is not null && lastValue is not null)
      {
        deltas.Add(new MeasurementDelta(metric, firstValue.Value, lastValue.Value, lastValue.Value - firstValue.Value, dates));
      }
    }
  }

  private static IReadOnlyDictionary<string, string?> Filters(params (string Key, string? Value)[] filters) =>
      new SortedDictionary<string, string?>(filters.ToDictionary(static filter => filter.Key, static filter => filter.Value, StringComparer.Ordinal), StringComparer.Ordinal);

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

  private sealed record UtcRange(int Weeks, DateTimeOffset Start, DateTimeOffset End);
  private sealed record WorkoutFetch(IReadOnlyList<Workout> Workouts, bool Truncated, string? Continuation);
}
