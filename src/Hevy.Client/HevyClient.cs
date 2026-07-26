using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Hevy.Client.Errors;
using Hevy.Client.Http;
using Hevy.Client.Models;
using Hevy.Client.Serialization;

namespace Hevy.Client;

public sealed class HevyClient : IHevyClient
{
  private readonly HttpClient httpClient;
  private readonly string apiKey;

  public HevyClient(HevyClientOptions options)
      : this(new HttpClient(CreateProductionPipeline(options), disposeHandler: true), options)
  {
  }

  internal HevyClient(HttpClient httpClient, HevyClientOptions options)
  {
    ArgumentNullException.ThrowIfNull(httpClient);
    ArgumentNullException.ThrowIfNull(options);

    httpClient.BaseAddress = HevyAuthenticationHandler.ApiOrigin;
    httpClient.DefaultRequestHeaders.Remove("api-key");
    this.httpClient = httpClient;
    apiKey = options.ApiKey;
  }

  internal static HevyRetryHandler CreateProductionPipeline(HevyClientOptions options)
  {
    ArgumentNullException.ThrowIfNull(options);
    return new HevyRetryHandler
    {
      InnerHandler = new HevyAuthenticationHandler(options)
      {
        InnerHandler = new HttpClientHandler
        {
          AllowAutoRedirect = false,
        },
      },
    };
  }

  public async Task<PagedResult<Workout>> GetWorkoutsAsync(int page, int pageSize, CancellationToken cancellationToken)
  {
    ValidatePagination(page, pageSize, 10);
    var response = await GetAsync($"v1/workouts?page={page}&pageSize={pageSize}", HevyJsonContext.Default.WorkoutPage, cancellationToken);
    return new PagedResult<Workout>(response.Page, response.PageCount, response.Workouts);
  }

  public async Task<int> GetWorkoutCountAsync(CancellationToken cancellationToken) =>
      (await GetAsync("v1/workouts/count", HevyJsonContext.Default.WorkoutCountResponse, cancellationToken)).WorkoutCount;

  public async Task<PagedResult<WorkoutEvent>> GetWorkoutEventsAsync(int page, int pageSize, DateTimeOffset since, CancellationToken cancellationToken)
  {
    ValidatePagination(page, pageSize, 10);
    var encodedSince = Uri.EscapeDataString(since.ToString("O", CultureInfo.InvariantCulture));
    var response = await GetAsync($"v1/workouts/events?page={page}&pageSize={pageSize}&since={encodedSince}", HevyJsonContext.Default.WorkoutEventsPage, cancellationToken);
    return new PagedResult<WorkoutEvent>(response.Page, response.PageCount, response.Events);
  }

  public Task<Workout> GetWorkoutAsync(string workoutId, CancellationToken cancellationToken) =>
      GetAsync($"v1/workouts/{EscapeIdentifier(workoutId, nameof(workoutId))}", HevyJsonContext.Default.Workout, cancellationToken);

  public async Task<UserInfo> GetUserInfoAsync(CancellationToken cancellationToken) =>
      (await GetAsync("v1/user/info", HevyJsonContext.Default.UserInfoResponse, cancellationToken)).Data;

  public async Task<PagedResult<Routine>> GetRoutinesAsync(int page, int pageSize, CancellationToken cancellationToken)
  {
    ValidatePagination(page, pageSize, 10);
    var response = await GetAsync($"v1/routines?page={page}&pageSize={pageSize}", HevyJsonContext.Default.RoutinePage, cancellationToken);
    return new PagedResult<Routine>(response.Page, response.PageCount, response.Routines);
  }

  public async Task<Routine> GetRoutineAsync(string routineId, CancellationToken cancellationToken) =>
      (await GetAsync($"v1/routines/{EscapeIdentifier(routineId, nameof(routineId))}", HevyJsonContext.Default.RoutineResponse, cancellationToken)).Routine;

  public async Task<PagedResult<ExerciseTemplate>> GetExerciseTemplatesAsync(int page, int pageSize, CancellationToken cancellationToken)
  {
    ValidatePagination(page, pageSize, 100);
    var response = await GetAsync($"v1/exercise_templates?page={page}&pageSize={pageSize}", HevyJsonContext.Default.ExerciseTemplatePage, cancellationToken);
    return new PagedResult<ExerciseTemplate>(response.Page, response.PageCount, response.ExerciseTemplates);
  }

  public Task<ExerciseTemplate> GetExerciseTemplateAsync(string exerciseTemplateId, CancellationToken cancellationToken) =>
      GetAsync($"v1/exercise_templates/{EscapeIdentifier(exerciseTemplateId, nameof(exerciseTemplateId))}", HevyJsonContext.Default.ExerciseTemplate, cancellationToken);

  public async Task<PagedResult<RoutineFolder>> GetRoutineFoldersAsync(int page, int pageSize, CancellationToken cancellationToken)
  {
    ValidatePagination(page, pageSize, 10);
    var response = await GetAsync($"v1/routine_folders?page={page}&pageSize={pageSize}", HevyJsonContext.Default.RoutineFolderPage, cancellationToken);
    return new PagedResult<RoutineFolder>(response.Page, response.PageCount, response.RoutineFolders);
  }

  public Task<RoutineFolder> GetRoutineFolderAsync(long folderId, CancellationToken cancellationToken) =>
      GetAsync($"v1/routine_folders/{folderId.ToString(CultureInfo.InvariantCulture)}", HevyJsonContext.Default.RoutineFolder, cancellationToken);

  public async Task<PagedResult<ExerciseHistoryEntry>> GetExerciseHistoryAsync(string exerciseTemplateId, int page, int pageSize, DateOnly? startDate, DateOnly? endDate, CancellationToken cancellationToken)
  {
    ValidatePagination(page, pageSize, 10);
    if (startDate is not null && endDate is not null && startDate > endDate)
    {
      throw new ArgumentException("The start date cannot be after the end date.", nameof(startDate));
    }

    var query = new List<string>();
    if (startDate is not null)
    {
      query.Add($"start_date={startDate.Value:yyyy-MM-dd}");
    }

    if (endDate is not null)
    {
      query.Add($"end_date={endDate.Value:yyyy-MM-dd}");
    }

    var path = $"v1/exercise_history/{EscapeIdentifier(exerciseTemplateId, nameof(exerciseTemplateId))}";
    if (query.Count > 0)
    {
      path += $"?{string.Join("&", query)}";
    }

    var response = await GetAsync(path, HevyJsonContext.Default.ExerciseHistoryResponse, cancellationToken);
    var itemCount = response.ExerciseHistory.Count;
    var pageCount = itemCount == 0 ? 0 : ((itemCount - 1) / pageSize) + 1;
    var offset = (long)(page - 1) * pageSize;
    var items = offset >= itemCount
        ? Array.Empty<ExerciseHistoryEntry>()
        : response.ExerciseHistory.Skip((int)offset).Take(pageSize).ToArray();
    return new PagedResult<ExerciseHistoryEntry>(page, pageCount, items);
  }

  public async Task<PagedResult<BodyMeasurement>> GetBodyMeasurementsAsync(int page, int pageSize, CancellationToken cancellationToken)
  {
    ValidatePagination(page, pageSize, 10);
    var response = await GetAsync($"v1/body_measurements?page={page}&pageSize={pageSize}", HevyJsonContext.Default.BodyMeasurementPage, cancellationToken);
    return new PagedResult<BodyMeasurement>(response.Page, response.PageCount, response.BodyMeasurements);
  }

  public Task<BodyMeasurement> GetBodyMeasurementAsync(DateOnly date, CancellationToken cancellationToken) =>
      GetAsync($"v1/body_measurements/{date:yyyy-MM-dd}", HevyJsonContext.Default.BodyMeasurement, cancellationToken);

  public Task<Workout> CreateWorkoutAsync(CreateWorkoutRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ValidateWorkout(request.Workout);
    return SendMutationAsync(HttpMethod.Post, "v1/workouts", request, HevyJsonContext.Default.CreateWorkoutRequest, HevyJsonContext.Default.Workout, retrySafe: false, cancellationToken);
  }

  public Task<Workout> UpdateWorkoutAsync(string workoutId, UpdateWorkoutRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ValidateWorkout(request.Workout);
    return SendMutationAsync(HttpMethod.Put, $"v1/workouts/{EscapeIdentifier(workoutId, nameof(workoutId))}", request, HevyJsonContext.Default.UpdateWorkoutRequest, HevyJsonContext.Default.Workout, retrySafe: false, cancellationToken);
  }

  public Task<Routine> CreateRoutineAsync(CreateRoutineRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ValidateRoutine(request.Routine);
    return SendMutationAsync(HttpMethod.Post, "v1/routines", request, HevyJsonContext.Default.CreateRoutineRequest, HevyJsonContext.Default.Routine, retrySafe: false, cancellationToken);
  }

  public async Task<Routine> UpdateRoutineAsync(string routineId, UpdateRoutineRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ValidateRoutine(request.Routine);
    return await SendMutationAsync(HttpMethod.Put, $"v1/routines/{EscapeIdentifier(routineId, nameof(routineId))}", request, HevyJsonContext.Default.UpdateRoutineRequest, HevyJsonContext.Default.Routine, retrySafe: false, cancellationToken);
  }

  public Task<RoutineFolder> CreateRoutineFolderAsync(CreateRoutineFolderRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(request.RoutineFolder);
    ValidateRequiredText(request.RoutineFolder.Title, "routine folder title");
    return SendMutationAsync(HttpMethod.Post, "v1/routine_folders", request, HevyJsonContext.Default.CreateRoutineFolderRequest, HevyJsonContext.Default.RoutineFolder, retrySafe: false, cancellationToken);
  }

  public async Task<ExerciseTemplate> CreateExerciseTemplateAsync(CreateExerciseTemplateRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ValidateExerciseTemplate(request.Exercise);
    var response = await SendMutationAsync(HttpMethod.Post, "v1/exercise_templates", request, HevyJsonContext.Default.CreateExerciseTemplateRequest, HevyJsonContext.Default.CreateExerciseTemplateResponse, retrySafe: false, cancellationToken);
    return await GetExerciseTemplateAsync(response.Id.ToString(CultureInfo.InvariantCulture), cancellationToken);
  }

  public async Task<BodyMeasurement> CreateBodyMeasurementAsync(CreateBodyMeasurementRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ValidateMeasurementDate(request.Date, nameof(request));
    ValidateMeasurementValues(request.WeightKg, request.LeanMassKg, request.FatPercent, request.NeckCm, request.ShoulderCm, request.ChestCm, request.LeftBicepCm, request.RightBicepCm, request.LeftForearmCm, request.RightForearmCm, request.Abdomen, request.Waist, request.Hips, request.LeftThigh, request.RightThigh, request.LeftCalf, request.RightCalf);
    await SendMutationWithoutResponseAsync(HttpMethod.Post, "v1/body_measurements", request, HevyJsonContext.Default.CreateBodyMeasurementRequest, retrySafe: false, cancellationToken);
    return await GetBodyMeasurementAsync(request.Date, cancellationToken);
  }

  public async Task<BodyMeasurement> UpdateBodyMeasurementAsync(DateOnly date, UpdateBodyMeasurementRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ValidateMeasurementDate(date, nameof(date));
    ValidateMeasurementValues(request.WeightKg, request.LeanMassKg, request.FatPercent, request.NeckCm, request.ShoulderCm, request.ChestCm, request.LeftBicepCm, request.RightBicepCm, request.LeftForearmCm, request.RightForearmCm, request.Abdomen, request.Waist, request.Hips, request.LeftThigh, request.RightThigh, request.LeftCalf, request.RightCalf);
    await SendMutationWithoutResponseAsync(HttpMethod.Put, $"v1/body_measurements/{date:yyyy-MM-dd}", request, HevyJsonContext.Default.UpdateBodyMeasurementRequest, retrySafe: true, cancellationToken);
    return await GetBodyMeasurementAsync(date, cancellationToken);
  }

  private async Task<T> GetAsync<T>(string relativeUri, JsonTypeInfo<T> jsonTypeInfo, CancellationToken cancellationToken)
  {
    var finalUri = httpClient.BaseAddress is null ? null : new Uri(httpClient.BaseAddress, relativeUri);
    HevyAuthenticationHandler.EnsureSafeTarget(finalUri);
    using var request = new HttpRequestMessage(HttpMethod.Get, relativeUri);
    request.Headers.TryAddWithoutValidation("api-key", apiKey);
    SetRetryDeadline(request);

    try
    {
      using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
      return await HevyResponse.ReadAsync(response, jsonTypeInfo, cancellationToken);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (HttpRequestException)
    {
      throw new HevyException("transient_upstream", "The Hevy API is temporarily unavailable.", true, null);
    }
  }

  private async Task<TResponse> SendMutationAsync<TRequest, TResponse>(HttpMethod method, string relativeUri, TRequest requestBody, JsonTypeInfo<TRequest> requestTypeInfo, JsonTypeInfo<TResponse> responseTypeInfo, bool retrySafe, CancellationToken cancellationToken)
  {
    using var request = CreateMutationRequest(method, relativeUri, requestBody, requestTypeInfo, retrySafe);
    try
    {
      using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
      ThrowIfMutationOutcomeUnknown(response);
      return await HevyResponse.ReadAsync(response, responseTypeInfo, cancellationToken);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (HttpRequestException)
    {
      throw new HevyOutcomeUnknownException();
    }
  }

  private async Task SendMutationWithoutResponseAsync<TRequest>(HttpMethod method, string relativeUri, TRequest requestBody, JsonTypeInfo<TRequest> requestTypeInfo, bool retrySafe, CancellationToken cancellationToken)
  {
    using var request = CreateMutationRequest(method, relativeUri, requestBody, requestTypeInfo, retrySafe);
    try
    {
      using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
      ThrowIfMutationOutcomeUnknown(response);
      HevyResponse.EnsureSuccess(response);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (HttpRequestException)
    {
      throw new HevyOutcomeUnknownException();
    }
  }

  private HttpRequestMessage CreateMutationRequest<TRequest>(HttpMethod method, string relativeUri, TRequest requestBody, JsonTypeInfo<TRequest> requestTypeInfo, bool retrySafe)
  {
    var finalUri = httpClient.BaseAddress is null ? null : new Uri(httpClient.BaseAddress, relativeUri);
    HevyAuthenticationHandler.EnsureSafeTarget(finalUri);
    var payload = JsonSerializer.SerializeToUtf8Bytes(requestBody, requestTypeInfo);
    var request = new HttpRequestMessage(method, relativeUri);
    request.Headers.TryAddWithoutValidation("api-key", apiKey);
    request.Content = new ByteArrayContent(payload);
    request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
    if (retrySafe)
    {
      request.Options.Set(HevyRetryHandler.RetrySafeMutation, true);
    }

    SetRetryDeadline(request);

    return request;
  }

  private static void ThrowIfMutationOutcomeUnknown(HttpResponseMessage response)
  {
    if ((int)response.StatusCode >= 500)
    {
      throw new HevyOutcomeUnknownException(response.StatusCode);
    }
  }

  private void SetRetryDeadline(HttpRequestMessage request)
  {
    if (httpClient.Timeout != Timeout.InfiniteTimeSpan)
    {
      request.Options.Set(HevyRetryHandler.RetryDeadline, DateTimeOffset.UtcNow + httpClient.Timeout);
    }
  }

  private static void ValidateWorkout(WorkoutWrite workout)
  {
    ArgumentNullException.ThrowIfNull(workout);
    ValidateRequiredText(workout.Title, "workout title");
    if (workout.EndTime < workout.StartTime)
    {
      throw new ArgumentException("Workout end time cannot be before its start time.", nameof(workout));
    }

    ArgumentNullException.ThrowIfNull(workout.Exercises);
    foreach (var exercise in workout.Exercises)
    {
      ArgumentNullException.ThrowIfNull(exercise);
      ValidateRequiredText(exercise.ExerciseTemplateId, "exercise template id");
      ArgumentNullException.ThrowIfNull(exercise.Sets);
      foreach (var set in exercise.Sets)
      {
        ArgumentNullException.ThrowIfNull(set);
      }
    }
  }

  private static void ValidateRoutine(CreateRoutineWrite routine)
  {
    ArgumentNullException.ThrowIfNull(routine);
    ValidateRequiredText(routine.Title, "routine title");
    ArgumentNullException.ThrowIfNull(routine.Exercises);
    foreach (var exercise in routine.Exercises)
    {
      ArgumentNullException.ThrowIfNull(exercise);
      ValidateRequiredText(exercise.ExerciseTemplateId, "exercise template id");
      ArgumentNullException.ThrowIfNull(exercise.Sets);
      foreach (var set in exercise.Sets)
      {
        ArgumentNullException.ThrowIfNull(set);
      }
    }
  }

  private static void ValidateRoutine(UpdateRoutineWrite routine)
  {
    ArgumentNullException.ThrowIfNull(routine);
    ValidateRequiredText(routine.Title, "routine title");
    ArgumentNullException.ThrowIfNull(routine.Exercises);
    foreach (var exercise in routine.Exercises)
    {
      ArgumentNullException.ThrowIfNull(exercise);
      ValidateRequiredText(exercise.ExerciseTemplateId, "exercise template id");
      ArgumentNullException.ThrowIfNull(exercise.Sets);
      foreach (var set in exercise.Sets)
      {
        ArgumentNullException.ThrowIfNull(set);
      }
    }
  }

  private static void ValidateExerciseTemplate(CustomExerciseWrite exercise)
  {
    ArgumentNullException.ThrowIfNull(exercise);
    ValidateRequiredText(exercise.Title, "exercise title");
    ArgumentNullException.ThrowIfNull(exercise.OtherMuscles);
    if (!Enum.IsDefined(exercise.ExerciseType) || !Enum.IsDefined(exercise.EquipmentCategory) || !Enum.IsDefined(exercise.MuscleGroup) || exercise.OtherMuscles.Any(muscle => !Enum.IsDefined(muscle)))
    {
      throw new ArgumentOutOfRangeException(nameof(exercise), "Exercise fields must use documented enum values.");
    }
  }

  private static void ValidateMeasurementDate(DateOnly date, string parameterName)
  {
    if (date == DateOnly.MinValue)
    {
      throw new ArgumentOutOfRangeException(parameterName, "A measurement date is required.");
    }
  }

  private static void ValidateMeasurementValues(params decimal?[] values)
  {
    if (values.Any(value => value is < 0))
    {
      throw new ArgumentOutOfRangeException(nameof(values), "Measurement values cannot be negative.");
    }
  }

  private static void ValidateRequiredText(string value, string fieldName)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      throw new ArgumentException($"A {fieldName} is required.", fieldName);
    }
  }

  private static string EscapeIdentifier(string value, string parameterName)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      throw new ArgumentException("An identifier is required.", parameterName);
    }

    return Uri.EscapeDataString(value);
  }

  private static void ValidatePagination(int page, int pageSize, int maximumPageSize)
  {
    ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
    ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, maximumPageSize);
  }
}
