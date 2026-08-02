namespace TestSupport;

public sealed class FakeHevyClient : IHevyClient
{
  public List<string> Operations { get; } = [];
  public int CallCount => Operations.Count;
  public string? LastOperation => Operations.LastOrDefault();
  public CancellationToken LastCancellationToken { get; private set; }

  public Func<int, int, CancellationToken, Task<PagedResult<Workout>>>? GetWorkoutsHandler { get; set; }
  public Func<int, int, DateTimeOffset, CancellationToken, Task<PagedResult<WorkoutEvent>>>? GetWorkoutEventsHandler { get; set; }
  public Func<string, CancellationToken, Task<Workout>>? GetWorkoutHandler { get; set; }
  public Func<string, CancellationToken, Task<Routine>>? GetRoutineHandler { get; set; }
  public Func<int, int, CancellationToken, Task<PagedResult<Routine>>>? GetRoutinesHandler { get; set; }
  public Func<int, int, CancellationToken, Task<PagedResult<ExerciseTemplate>>>? GetExerciseTemplatesHandler { get; set; }
  public Func<string, ExerciseHistoryQuery, CancellationToken, Task<ExerciseHistoryWindow>>? GetExerciseHistoryWindowHandler { get; set; }
  public Func<int, int, CancellationToken, Task<PagedResult<BodyMeasurement>>>? GetBodyMeasurementsHandler { get; set; }
  public Func<DateOnly, CancellationToken, Task<BodyMeasurement>>? GetBodyMeasurementHandler { get; set; }
  public Func<CreateExerciseTemplateCommand, CancellationToken, Task<ExerciseTemplate>>? CreateExerciseTemplateHandler { get; set; }
  public Func<CreateRoutineCommand, CancellationToken, Task<Routine>>? CreateRoutineHandler { get; set; }
  public Func<string, UpdateRoutineCommand, CancellationToken, Task<Routine>>? UpdateRoutineHandler { get; set; }

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
  public ImmutableList<ExerciseHistoryEntry> ExerciseHistory { get; set; } = [];
  public PagedResult<BodyMeasurement> BodyMeasurements { get; set; } = new(1, 0, []);
  public BodyMeasurement BodyMeasurement { get; set; } = SampleMeasurement();

  public Task<PagedResult<Workout>> GetWorkoutsAsync(int page, int pageSize, CancellationToken cancellationToken)
  {
    Record(nameof(GetWorkoutsAsync), cancellationToken);
    return GetWorkoutsHandler?.Invoke(page, pageSize, cancellationToken) ?? Task.FromResult(Workouts);
  }

  public Task<int> GetWorkoutCountAsync(CancellationToken cancellationToken) => Return(nameof(GetWorkoutCountAsync), WorkoutCount, cancellationToken);
  public Task<PagedResult<WorkoutEvent>> GetWorkoutEventsAsync(int page, int pageSize, DateTimeOffset since, CancellationToken cancellationToken)
  {
    Record(nameof(GetWorkoutEventsAsync), cancellationToken);
    return GetWorkoutEventsHandler?.Invoke(page, pageSize, since, cancellationToken) ?? Task.FromResult(WorkoutEvents);
  }

  public Task<Workout> GetWorkoutAsync(string workoutId, CancellationToken cancellationToken)
  {
    Record(nameof(GetWorkoutAsync), cancellationToken);
    return GetWorkoutHandler?.Invoke(workoutId, cancellationToken) ?? Task.FromResult(Workout);
  }

  public Task<UserInfo> GetUserInfoAsync(CancellationToken cancellationToken) => Return(nameof(GetUserInfoAsync), UserInfo, cancellationToken);
  public Task<PagedResult<Routine>> GetRoutinesAsync(int page, int pageSize, CancellationToken cancellationToken)
  {
    Record(nameof(GetRoutinesAsync), cancellationToken);
    return GetRoutinesHandler?.Invoke(page, pageSize, cancellationToken) ?? Task.FromResult(Routines);
  }
  public Task<Routine> GetRoutineAsync(string routineId, CancellationToken cancellationToken)
  {
    Record(nameof(GetRoutineAsync), cancellationToken);
    return GetRoutineHandler?.Invoke(routineId, cancellationToken) ?? Task.FromResult(Routine);
  }

  public Task<PagedResult<ExerciseTemplate>> GetExerciseTemplatesAsync(int page, int pageSize, CancellationToken cancellationToken)
  {
    Record(nameof(GetExerciseTemplatesAsync), cancellationToken);
    return GetExerciseTemplatesHandler?.Invoke(page, pageSize, cancellationToken) ?? Task.FromResult(ExerciseTemplates);
  }
  public Task<ExerciseTemplate> GetExerciseTemplateAsync(string exerciseTemplateId, CancellationToken cancellationToken) => Return(nameof(GetExerciseTemplateAsync), ExerciseTemplate, cancellationToken);
  public Task<PagedResult<RoutineFolder>> GetRoutineFoldersAsync(int page, int pageSize, CancellationToken cancellationToken) => Return(nameof(GetRoutineFoldersAsync), RoutineFolders, cancellationToken);
  public Task<RoutineFolder> GetRoutineFolderAsync(long folderId, CancellationToken cancellationToken) => Return(nameof(GetRoutineFolderAsync), RoutineFolder, cancellationToken);
  public Task<ExerciseHistoryWindow> GetExerciseHistoryAsync(string exerciseTemplateId, int page, int pageSize, DateOnly? startDate, DateOnly? endDate, CancellationToken cancellationToken)
  {
    Record(nameof(GetExerciseHistoryAsync), cancellationToken);
    var request = new ExerciseHistoryQuery(ExerciseHistoryQuery.PageOffset(page, pageSize), pageSize, startDate, endDate);
    return GetExerciseHistoryWindowHandler?.Invoke(exerciseTemplateId, request, cancellationToken) ?? Task.FromResult(Window(request));
  }

  public Task<ExerciseHistoryWindow> GetExerciseHistoryWindowAsync(string exerciseTemplateId, ExerciseHistoryQuery request, CancellationToken cancellationToken)
  {
    Record(nameof(GetExerciseHistoryWindowAsync), cancellationToken);
    return GetExerciseHistoryWindowHandler?.Invoke(exerciseTemplateId, request, cancellationToken) ?? Task.FromResult(Window(request));
  }

  private ExerciseHistoryWindow Window(ExerciseHistoryQuery request)
  {
    var source = ExerciseHistory
        .Where(entry => (request.EligibleStartTime is null || entry.WorkoutStartTime >= request.EligibleStartTime) &&
                        (request.EligibleEndTime is null || entry.WorkoutStartTime < request.EligibleEndTime))
        .ToImmutableList();
    var items = source.Skip(request.Offset).Take(request.Limit).ToImmutableList();
    var truncated = request.Offset + items.Count < source.Count;
    var terminal = truncated && request.Offset + request.Limit >= ExerciseHistoryQuery.MaximumScannedItems
        ? ExerciseHistoryWindow.ItemSafetyCap
        : null;
    return new ExerciseHistoryWindow(items, truncated, Math.Min(source.Count, ExerciseHistoryQuery.MaximumScannedItems), terminal);
  }

  public Task<PagedResult<BodyMeasurement>> GetBodyMeasurementsAsync(int page, int pageSize, CancellationToken cancellationToken)
  {
    Record(nameof(GetBodyMeasurementsAsync), cancellationToken);
    return GetBodyMeasurementsHandler?.Invoke(page, pageSize, cancellationToken) ?? Task.FromResult(BodyMeasurements);
  }
  public Task<BodyMeasurement> GetBodyMeasurementAsync(DateOnly date, CancellationToken cancellationToken)
  {
    Record(nameof(GetBodyMeasurementAsync), cancellationToken);
    return GetBodyMeasurementHandler?.Invoke(date, cancellationToken) ?? Task.FromResult(BodyMeasurement);
  }

  public Task<Workout> CreateWorkoutAsync(CreateWorkoutCommand request, CancellationToken cancellationToken) => Return(nameof(CreateWorkoutAsync), Workout, cancellationToken);
  public Task<Workout> UpdateWorkoutAsync(string workoutId, UpdateWorkoutCommand request, CancellationToken cancellationToken) => Return(nameof(UpdateWorkoutAsync), Workout, cancellationToken);
  public Task<Routine> CreateRoutineAsync(CreateRoutineCommand request, CancellationToken cancellationToken)
  {
    Record(nameof(CreateRoutineAsync), cancellationToken);
    return CreateRoutineHandler?.Invoke(request, cancellationToken) ?? Task.FromResult(Routine);
  }
  public Task<Routine> UpdateRoutineAsync(string routineId, UpdateRoutineCommand request, CancellationToken cancellationToken)
  {
    Record(nameof(UpdateRoutineAsync), cancellationToken);
    return UpdateRoutineHandler?.Invoke(routineId, request, cancellationToken) ?? Task.FromResult(Routine);
  }
  public Task<RoutineFolder> CreateRoutineFolderAsync(CreateRoutineFolderCommand request, CancellationToken cancellationToken) => Return(nameof(CreateRoutineFolderAsync), RoutineFolder, cancellationToken);
  public Task<ExerciseTemplate> CreateExerciseTemplateAsync(CreateExerciseTemplateCommand request, CancellationToken cancellationToken)
  {
    Record(nameof(CreateExerciseTemplateAsync), cancellationToken);
    return CreateExerciseTemplateHandler?.Invoke(request, cancellationToken) ?? Task.FromResult(ExerciseTemplate);
  }
  public Task<BodyMeasurement> CreateBodyMeasurementAsync(CreateBodyMeasurementCommand request, CancellationToken cancellationToken) => Return(nameof(CreateBodyMeasurementAsync), BodyMeasurement, cancellationToken);
  public Task<BodyMeasurement> UpdateBodyMeasurementAsync(DateOnly date, UpdateBodyMeasurementCommand request, CancellationToken cancellationToken) => Return(nameof(UpdateBodyMeasurementAsync), BodyMeasurement, cancellationToken);

  private Task<T> Return<T>(string operation, T result, CancellationToken token)
  {
    Record(operation, token);
    return Task.FromResult(result);
  }

  private void Record(string operation, CancellationToken token)
  {
    Operations.Add(operation);
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
