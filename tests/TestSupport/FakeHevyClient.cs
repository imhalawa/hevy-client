using Hevy.Client;
using Hevy.Client.Models;

namespace TestSupport;

public sealed class FakeHevyClient : IHevyClient
{
  public int CallCount { get; private set; }
  public string? LastOperation { get; private set; }
  public object? LastRequest { get; private set; }
  public CancellationToken LastCancellationToken { get; private set; }

  public Func<int, int, CancellationToken, Task<PagedResult<Workout>>>? GetWorkoutsHandler { get; set; }
  public Func<int, int, DateTimeOffset, CancellationToken, Task<PagedResult<WorkoutEvent>>>? GetWorkoutEventsHandler { get; set; }
  public Func<string, CancellationToken, Task<Workout>>? GetWorkoutHandler { get; set; }
  public Func<string, CancellationToken, Task<Routine>>? GetRoutineHandler { get; set; }
  public Func<int, int, CancellationToken, Task<PagedResult<Routine>>>? GetRoutinesHandler { get; set; }
  public Func<int, int, CancellationToken, Task<PagedResult<ExerciseTemplate>>>? GetExerciseTemplatesHandler { get; set; }
  public Func<string, ExerciseHistoryWindowRequest, CancellationToken, Task<ExerciseHistoryWindow>>? GetExerciseHistoryWindowHandler { get; set; }
  public Func<int, int, CancellationToken, Task<PagedResult<BodyMeasurement>>>? GetBodyMeasurementsHandler { get; set; }
  public Func<DateOnly, CancellationToken, Task<BodyMeasurement>>? GetBodyMeasurementHandler { get; set; }
  public Func<CreateExerciseTemplateRequest, CancellationToken, Task<ExerciseTemplate>>? CreateExerciseTemplateHandler { get; set; }

  public PagedResult<Workout> Workouts { get; set; } = new(1, 0, []);
  public int WorkoutCount { get; set; }
  public PagedResult<WorkoutEvent> WorkoutEvents { get; set; } = new(1, 0, []);
  public Workout Workout { get; set; } = SampleWorkout();
  public UserInfo UserInfo { get; set; } = new("user-1", "Ada", "https://hevy.com/user/ada");
  public PagedResult<Routine> Routines { get; set; } = new(1, 0, []);
  public Routine Routine { get; set; } = SampleRoutine();
  public PagedResult<ExerciseTemplate> ExerciseTemplates { get; set; } = new(1, 0, []);
  public ExerciseTemplate ExerciseTemplate { get; set; } = new("template-1", "Squat", "weight_reps", "quadriceps", ["glutes"], EquipmentCategory.Barbell, false);
  public PagedResult<RoutineFolder> RoutineFolders { get; set; } = new(1, 0, []);
  public RoutineFolder RoutineFolder { get; set; } = new(1, 0, "Legs", DateTimeOffset.Parse("2026-07-25T12:00:00Z"), DateTimeOffset.Parse("2026-07-01T12:00:00Z"));
  public PagedResult<ExerciseHistoryEntry> ExerciseHistory { get; set; } = new(1, 0, []);
  public IReadOnlyList<ExerciseHistoryEntry>? AllExerciseHistory { get; set; }
  public PagedResult<BodyMeasurement> BodyMeasurements { get; set; } = new(1, 0, []);
  public BodyMeasurement BodyMeasurement { get; set; } = SampleMeasurement();

  public Task<PagedResult<Workout>> GetWorkoutsAsync(int page, int pageSize, CancellationToken cancellationToken)
  {
    Record(nameof(GetWorkoutsAsync), new { page, pageSize }, cancellationToken);
    return GetWorkoutsHandler?.Invoke(page, pageSize, cancellationToken) ?? Task.FromResult(Workouts);
  }

  public Task<int> GetWorkoutCountAsync(CancellationToken cancellationToken) => Return(nameof(GetWorkoutCountAsync), WorkoutCount, cancellationToken);
  public Task<PagedResult<WorkoutEvent>> GetWorkoutEventsAsync(int page, int pageSize, DateTimeOffset since, CancellationToken cancellationToken)
  {
    Record(nameof(GetWorkoutEventsAsync), new { page, pageSize, since }, cancellationToken);
    return GetWorkoutEventsHandler?.Invoke(page, pageSize, since, cancellationToken) ?? Task.FromResult(WorkoutEvents);
  }

  public Task<Workout> GetWorkoutAsync(string workoutId, CancellationToken cancellationToken)
  {
    Record(nameof(GetWorkoutAsync), workoutId, cancellationToken);
    return GetWorkoutHandler?.Invoke(workoutId, cancellationToken) ?? Task.FromResult(Workout);
  }

  public Task<UserInfo> GetUserInfoAsync(CancellationToken cancellationToken) => Return(nameof(GetUserInfoAsync), UserInfo, cancellationToken);
  public Task<PagedResult<Routine>> GetRoutinesAsync(int page, int pageSize, CancellationToken cancellationToken)
  {
    Record(nameof(GetRoutinesAsync), new { page, pageSize }, cancellationToken);
    return GetRoutinesHandler?.Invoke(page, pageSize, cancellationToken) ?? Task.FromResult(Routines);
  }
  public Task<Routine> GetRoutineAsync(string routineId, CancellationToken cancellationToken)
  {
    Record(nameof(GetRoutineAsync), routineId, cancellationToken);
    return GetRoutineHandler?.Invoke(routineId, cancellationToken) ?? Task.FromResult(Routine);
  }

  public Task<PagedResult<ExerciseTemplate>> GetExerciseTemplatesAsync(int page, int pageSize, CancellationToken cancellationToken)
  {
    Record(nameof(GetExerciseTemplatesAsync), new { page, pageSize }, cancellationToken);
    return GetExerciseTemplatesHandler?.Invoke(page, pageSize, cancellationToken) ?? Task.FromResult(ExerciseTemplates);
  }
  public Task<ExerciseTemplate> GetExerciseTemplateAsync(string exerciseTemplateId, CancellationToken cancellationToken) => Return(nameof(GetExerciseTemplateAsync), ExerciseTemplate, cancellationToken, exerciseTemplateId);
  public Task<PagedResult<RoutineFolder>> GetRoutineFoldersAsync(int page, int pageSize, CancellationToken cancellationToken) => Return(nameof(GetRoutineFoldersAsync), RoutineFolders, cancellationToken, new { page, pageSize });
  public Task<RoutineFolder> GetRoutineFolderAsync(long folderId, CancellationToken cancellationToken) => Return(nameof(GetRoutineFolderAsync), RoutineFolder, cancellationToken, folderId);
  public Task<ExerciseHistoryWindow> GetExerciseHistoryAsync(string exerciseTemplateId, int page, int pageSize, DateOnly? startDate, DateOnly? endDate, CancellationToken cancellationToken)
  {
    Record(nameof(GetExerciseHistoryAsync), new { exerciseTemplateId, page, pageSize, startDate, endDate }, cancellationToken);
    var request = new ExerciseHistoryWindowRequest(ExerciseHistoryWindowRequest.PageOffset(page, pageSize), pageSize, startDate, endDate);
    return GetExerciseHistoryWindowHandler?.Invoke(exerciseTemplateId, request, cancellationToken) ?? Task.FromResult(Window(request));
  }

  public Task<ExerciseHistoryWindow> GetExerciseHistoryWindowAsync(string exerciseTemplateId, ExerciseHistoryWindowRequest request, CancellationToken cancellationToken)
  {
    Record(nameof(GetExerciseHistoryWindowAsync), new { exerciseTemplateId, request }, cancellationToken);
    return GetExerciseHistoryWindowHandler?.Invoke(exerciseTemplateId, request, cancellationToken) ?? Task.FromResult(Window(request));
  }

  private ExerciseHistoryWindow Window(ExerciseHistoryWindowRequest request)
  {
    var source = (AllExerciseHistory ?? ExerciseHistory.Items)
        .Where(entry => (request.EligibleStartTime is null || entry.WorkoutStartTime >= request.EligibleStartTime) &&
                        (request.EligibleEndTime is null || entry.WorkoutStartTime < request.EligibleEndTime))
        .ToArray();
    var items = source.Skip(request.Offset).Take(request.Limit).ToArray();
    var truncated = request.Offset + items.Length < source.Length;
    var terminal = truncated && request.Offset + request.Limit >= ExerciseHistoryWindowRequest.MaximumScannedItems
        ? ExerciseHistoryWindow.ItemSafetyCap
        : null;
    return new ExerciseHistoryWindow(items, truncated, Math.Min(source.Length, ExerciseHistoryWindowRequest.MaximumScannedItems), terminal);
  }

  public Task<PagedResult<BodyMeasurement>> GetBodyMeasurementsAsync(int page, int pageSize, CancellationToken cancellationToken)
  {
    Record(nameof(GetBodyMeasurementsAsync), new { page, pageSize }, cancellationToken);
    return GetBodyMeasurementsHandler?.Invoke(page, pageSize, cancellationToken) ?? Task.FromResult(BodyMeasurements);
  }
  public Task<BodyMeasurement> GetBodyMeasurementAsync(DateOnly date, CancellationToken cancellationToken)
  {
    Record(nameof(GetBodyMeasurementAsync), date, cancellationToken);
    return GetBodyMeasurementHandler?.Invoke(date, cancellationToken) ?? Task.FromResult(BodyMeasurement);
  }

  public Task<Workout> CreateWorkoutAsync(CreateWorkoutRequest request, CancellationToken cancellationToken) => Return(nameof(CreateWorkoutAsync), Workout, cancellationToken, request);
  public Task<Workout> UpdateWorkoutAsync(string workoutId, UpdateWorkoutRequest request, CancellationToken cancellationToken) => Return(nameof(UpdateWorkoutAsync), Workout, cancellationToken, new { workoutId, request });
  public Task<Routine> CreateRoutineAsync(CreateRoutineRequest request, CancellationToken cancellationToken) => Return(nameof(CreateRoutineAsync), Routine, cancellationToken, request);
  public Task<Routine> UpdateRoutineAsync(string routineId, UpdateRoutineRequest request, CancellationToken cancellationToken) => Return(nameof(UpdateRoutineAsync), Routine, cancellationToken, new { routineId, request });
  public Task<RoutineFolder> CreateRoutineFolderAsync(CreateRoutineFolderRequest request, CancellationToken cancellationToken) => Return(nameof(CreateRoutineFolderAsync), RoutineFolder, cancellationToken, request);
  public Task<ExerciseTemplate> CreateExerciseTemplateAsync(CreateExerciseTemplateRequest request, CancellationToken cancellationToken)
  {
    Record(nameof(CreateExerciseTemplateAsync), request, cancellationToken);
    return CreateExerciseTemplateHandler?.Invoke(request, cancellationToken) ?? Task.FromResult(ExerciseTemplate);
  }
  public Task<BodyMeasurement> CreateBodyMeasurementAsync(CreateBodyMeasurementRequest request, CancellationToken cancellationToken) => Return(nameof(CreateBodyMeasurementAsync), BodyMeasurement, cancellationToken, request);
  public Task<BodyMeasurement> UpdateBodyMeasurementAsync(DateOnly date, UpdateBodyMeasurementRequest request, CancellationToken cancellationToken) => Return(nameof(UpdateBodyMeasurementAsync), BodyMeasurement, cancellationToken, new { date, request });

  private Task<T> Return<T>(string operation, T result, CancellationToken token, object? request = null)
  {
    Record(operation, request, token);
    return Task.FromResult(result);
  }

  private void Record(string operation, object? request, CancellationToken token)
  {
    CallCount++;
    LastOperation = operation;
    LastRequest = request;
    LastCancellationToken = token;
  }

  public static Workout SampleWorkout() => new(
      "workout-1", "Leg Day", "routine-1", "Hard", DateTimeOffset.Parse("2026-07-25T10:00:00Z"),
      DateTimeOffset.Parse("2026-07-25T11:00:00Z"), DateTimeOffset.Parse("2026-07-25T12:00:00Z"),
      DateTimeOffset.Parse("2026-07-25T10:00:00Z"),
      [new WorkoutExercise(0, "Squat", "Deep", "template-1", null, [new WorkoutSet(0, "normal", 100, 5, null, null, 8, null)])]);

  public static Routine SampleRoutine() => new(
      "routine-1", "Leg Day", 1, DateTimeOffset.Parse("2026-07-25T12:00:00Z"), DateTimeOffset.Parse("2026-07-01T12:00:00Z"),
      [new RoutineExercise(0, "Squat", "90", "Deep", "template-1", null, [new RoutineSet(0, "normal", 100, 5, null, null, null, null, new RepRange(5, 8))])]);

  public static BodyMeasurement SampleMeasurement() => new(new DateOnly(2026, 7, 25), 80, 65, 18, 38, 110, 95, 35, 35, 28, 28, 85, 80, 95, 55, 55, 37, 37);
}
