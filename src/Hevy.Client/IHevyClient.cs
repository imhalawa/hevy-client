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
  Task<PagedResult<BodyMeasurement>> GetBodyMeasurementsAsync(int page, int pageSize, CancellationToken cancellationToken);
  Task<BodyMeasurement> GetBodyMeasurementAsync(DateOnly date, CancellationToken cancellationToken);
}
