using Hevy.Client.Models;

namespace Hevy.Client;

public interface IHevyClient
{
  Task<PagedResult<Workout>> GetWorkoutsAsync(int page, int pageSize, CancellationToken cancellationToken);
  Task<int> GetWorkoutCountAsync(CancellationToken cancellationToken);
  Task<PagedResult<WorkoutEvent>> GetWorkoutEventsAsync(int page, int pageSize, DateTimeOffset since, CancellationToken cancellationToken);
  Task<Workout> GetWorkoutAsync(string workoutId, CancellationToken cancellationToken);
  Task<UserInfo> GetUserInfoAsync(CancellationToken cancellationToken);
  Task<PagedResult<Routine>> GetRoutinesAsync(int page, int pageSize, CancellationToken cancellationToken);
  Task<Routine> GetRoutineAsync(string routineId, CancellationToken cancellationToken);
  Task<PagedResult<ExerciseTemplate>> GetExerciseTemplatesAsync(int page, int pageSize, CancellationToken cancellationToken);
  Task<ExerciseTemplate> GetExerciseTemplateAsync(string exerciseTemplateId, CancellationToken cancellationToken);
  Task<PagedResult<RoutineFolder>> GetRoutineFoldersAsync(int page, int pageSize, CancellationToken cancellationToken);
  Task<RoutineFolder> GetRoutineFolderAsync(long folderId, CancellationToken cancellationToken);
  Task<PagedResult<ExerciseHistoryEntry>> GetExerciseHistoryAsync(string exerciseTemplateId, int page, int pageSize, DateOnly? startDate, DateOnly? endDate, CancellationToken cancellationToken);
  Task<IReadOnlyList<ExerciseHistoryEntry>> GetAllExerciseHistoryAsync(string exerciseTemplateId, DateOnly? startDate, DateOnly? endDate, CancellationToken cancellationToken);
  Task<PagedResult<BodyMeasurement>> GetBodyMeasurementsAsync(int page, int pageSize, CancellationToken cancellationToken);
  Task<BodyMeasurement> GetBodyMeasurementAsync(DateOnly date, CancellationToken cancellationToken);
  Task<Workout> CreateWorkoutAsync(CreateWorkoutRequest request, CancellationToken cancellationToken);
  Task<Workout> UpdateWorkoutAsync(string workoutId, UpdateWorkoutRequest request, CancellationToken cancellationToken);
  Task<Routine> CreateRoutineAsync(CreateRoutineRequest request, CancellationToken cancellationToken);
  Task<Routine> UpdateRoutineAsync(string routineId, UpdateRoutineRequest request, CancellationToken cancellationToken);
  Task<RoutineFolder> CreateRoutineFolderAsync(CreateRoutineFolderRequest request, CancellationToken cancellationToken);
  Task<ExerciseTemplate> CreateExerciseTemplateAsync(CreateExerciseTemplateRequest request, CancellationToken cancellationToken);
  Task<BodyMeasurement> CreateBodyMeasurementAsync(CreateBodyMeasurementRequest request, CancellationToken cancellationToken);
  Task<BodyMeasurement> UpdateBodyMeasurementAsync(DateOnly date, UpdateBodyMeasurementRequest request, CancellationToken cancellationToken);
}
