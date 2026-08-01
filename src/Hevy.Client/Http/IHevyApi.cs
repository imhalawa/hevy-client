using Refit;

namespace Hevy.Client.Http;

internal interface IHevyApi
{
  [Get("/v1/workouts")]
  Task<HttpResponseMessage> GetWorkoutsAsync(int page, [AliasAs("pageSize")] int pageSize, [Header("api-key")] string apiKey, CancellationToken cancellationToken);

  [Get("/v1/workouts/count")]
  Task<HttpResponseMessage> GetWorkoutCountAsync([Header("api-key")] string apiKey, CancellationToken cancellationToken);

  [Get("/v1/workouts/events")]
  Task<HttpResponseMessage> GetWorkoutEventsAsync(int page, [AliasAs("pageSize")] int pageSize, string since, [Header("api-key")] string apiKey, CancellationToken cancellationToken);

  [Get("/v1/workouts/{workoutId}")]
  Task<HttpResponseMessage> GetWorkoutAsync(string workoutId, [Header("api-key")] string apiKey, CancellationToken cancellationToken);

  [Get("/v1/user/info")]
  Task<HttpResponseMessage> GetUserInfoAsync([Header("api-key")] string apiKey, CancellationToken cancellationToken);

  [Get("/v1/routines")]
  Task<HttpResponseMessage> GetRoutinesAsync(int page, [AliasAs("pageSize")] int pageSize, [Header("api-key")] string apiKey, CancellationToken cancellationToken);

  [Get("/v1/routines/{routineId}")]
  Task<HttpResponseMessage> GetRoutineAsync(string routineId, [Header("api-key")] string apiKey, CancellationToken cancellationToken);

  [Get("/v1/exercise_templates")]
  Task<HttpResponseMessage> GetExerciseTemplatesAsync(int page, [AliasAs("pageSize")] int pageSize, [Header("api-key")] string apiKey, CancellationToken cancellationToken);

  [Get("/v1/exercise_templates/{exerciseTemplateId}")]
  Task<HttpResponseMessage> GetExerciseTemplateAsync(string exerciseTemplateId, [Header("api-key")] string apiKey, CancellationToken cancellationToken);

  [Get("/v1/routine_folders")]
  Task<HttpResponseMessage> GetRoutineFoldersAsync(int page, [AliasAs("pageSize")] int pageSize, [Header("api-key")] string apiKey, CancellationToken cancellationToken);

  [Get("/v1/routine_folders/{folderId}")]
  Task<HttpResponseMessage> GetRoutineFolderAsync(long folderId, [Header("api-key")] string apiKey, CancellationToken cancellationToken);

  [Get("/v1/exercise_history/{exerciseTemplateId}")]
  Task<HttpResponseMessage> GetExerciseHistoryAsync(
      string exerciseTemplateId,
      [AliasAs("start_date")] string? startDate,
      [AliasAs("end_date")] string? endDate,
      [Header("api-key")] string apiKey,
      CancellationToken cancellationToken);

  [Get("/v1/body_measurements")]
  Task<HttpResponseMessage> GetBodyMeasurementsAsync(int page, [AliasAs("pageSize")] int pageSize, [Header("api-key")] string apiKey, CancellationToken cancellationToken);

  [Get("/v1/body_measurements/{date}")]
  Task<HttpResponseMessage> GetBodyMeasurementAsync(string date, [Header("api-key")] string apiKey, CancellationToken cancellationToken);
}
