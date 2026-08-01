using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Hevy.Core.Exceptions;
using Hevy.Client.Contracts;
using Hevy.Client.Http;
using Hevy.Core.Models;
using Hevy.Client.Serialization;
using Refit;

namespace Hevy.Client;

public sealed class HevyClient : IHevyClient
{
  private readonly HttpClient httpClient;
  private readonly IHevyApi api;
  private readonly string apiKey;
  private readonly ExerciseHistoryReadLimits exerciseHistoryReadLimits;

  public HevyClient(HevyClientOptions options)
      : this(new HttpClient(CreateProductionPipeline(options), disposeHandler: true), options)
  {
  }

  internal HevyClient(HttpClient httpClient, HevyClientOptions options)
      : this(httpClient, options, ExerciseHistoryReadLimits.Default)
  {
  }

  internal HevyClient(HttpClient httpClient, HevyClientOptions options, ExerciseHistoryReadLimits exerciseHistoryReadLimits)
  {
    ArgumentNullException.ThrowIfNull(httpClient);
    ArgumentNullException.ThrowIfNull(options);
    ArgumentNullException.ThrowIfNull(exerciseHistoryReadLimits);
    exerciseHistoryReadLimits.Validate();

    httpClient.BaseAddress = HevyAuthenticationHandler.ApiOrigin;
    httpClient.DefaultRequestHeaders.Remove("api-key");
    this.httpClient = httpClient;
    api = RestService.For<IHevyApi>(httpClient);
    apiKey = options.ApiKey;
    this.exerciseHistoryReadLimits = exerciseHistoryReadLimits;
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
    var response = await GetAsync(token => api.GetWorkoutsAsync(page, pageSize, apiKey, token), HevyJsonContext.Default.WorkoutPageResponse, cancellationToken);
    ValidatePage(response.Page, response.PageCount, response.Workouts, page, pageSize);
    return new PagedResult<Workout>(response.Page, response.PageCount, response.Workouts.Select(HevyApiMapping.ToDomain).ToImmutableList());
  }

  public async Task<int> GetWorkoutCountAsync(CancellationToken cancellationToken) =>
      (await GetAsync(token => api.GetWorkoutCountAsync(apiKey, token), HevyJsonContext.Default.WorkoutCountResponse, cancellationToken)).WorkoutCount;

  public async Task<PagedResult<WorkoutEvent>> GetWorkoutEventsAsync(int page, int pageSize, DateTimeOffset since, CancellationToken cancellationToken)
  {
    ValidatePagination(page, pageSize, 10);
    var response = await GetAsync(token => api.GetWorkoutEventsAsync(page, pageSize, since.ToString("O", CultureInfo.InvariantCulture), apiKey, token), HevyJsonContext.Default.WorkoutEventsPageResponse, cancellationToken);
    ValidatePage(response.Page, response.PageCount, response.Events, page, pageSize);
    return new PagedResult<WorkoutEvent>(response.Page, response.PageCount, response.Events.Select(HevyApiMapping.ToDomain).ToImmutableList());
  }

  public async Task<Workout> GetWorkoutAsync(string workoutId, CancellationToken cancellationToken) =>
      (await GetAsync(token => api.GetWorkoutAsync(ValidateIdentifier(workoutId, nameof(workoutId)), apiKey, token), HevyJsonContext.Default.WorkoutResponse, cancellationToken)).ToDomain();

  public async Task<UserInfo> GetUserInfoAsync(CancellationToken cancellationToken) =>
      (await GetAsync(token => api.GetUserInfoAsync(apiKey, token), HevyJsonContext.Default.UserInfoResponse, cancellationToken)).Data.ToDomain();

  public async Task<PagedResult<Routine>> GetRoutinesAsync(int page, int pageSize, CancellationToken cancellationToken)
  {
    ValidatePagination(page, pageSize, 10);
    var response = await GetAsync(token => api.GetRoutinesAsync(page, pageSize, apiKey, token), HevyJsonContext.Default.RoutinePageResponse, cancellationToken);
    ValidatePage(response.Page, response.PageCount, response.Routines, page, pageSize);
    return new PagedResult<Routine>(response.Page, response.PageCount, response.Routines.Select(HevyApiMapping.ToDomain).ToImmutableList());
  }

  public async Task<Routine> GetRoutineAsync(string routineId, CancellationToken cancellationToken) =>
      (await GetAsync(token => api.GetRoutineAsync(ValidateIdentifier(routineId, nameof(routineId)), apiKey, token), HevyJsonContext.Default.RoutineEnvelopeResponse, cancellationToken)).Routine.ToDomain();

  public async Task<PagedResult<ExerciseTemplate>> GetExerciseTemplatesAsync(int page, int pageSize, CancellationToken cancellationToken)
  {
    ValidatePagination(page, pageSize, 100);
    var response = await GetAsync(token => api.GetExerciseTemplatesAsync(page, pageSize, apiKey, token), HevyJsonContext.Default.ExerciseTemplatePageResponse, cancellationToken);
    ValidatePage(response.Page, response.PageCount, response.ExerciseTemplates, page, pageSize);
    return new PagedResult<ExerciseTemplate>(response.Page, response.PageCount, response.ExerciseTemplates.Select(HevyApiMapping.ToDomain).ToImmutableList());
  }

  public async Task<ExerciseTemplate> GetExerciseTemplateAsync(string exerciseTemplateId, CancellationToken cancellationToken) =>
      (await GetAsync(token => api.GetExerciseTemplateAsync(ValidateIdentifier(exerciseTemplateId, nameof(exerciseTemplateId)), apiKey, token), HevyJsonContext.Default.ExerciseTemplateResponse, cancellationToken)).ToDomain();

  public async Task<PagedResult<RoutineFolder>> GetRoutineFoldersAsync(int page, int pageSize, CancellationToken cancellationToken)
  {
    ValidatePagination(page, pageSize, 10);
    var response = await GetAsync(token => api.GetRoutineFoldersAsync(page, pageSize, apiKey, token), HevyJsonContext.Default.RoutineFolderPageResponse, cancellationToken);
    ValidatePage(response.Page, response.PageCount, response.RoutineFolders, page, pageSize);
    return new PagedResult<RoutineFolder>(response.Page, response.PageCount, response.RoutineFolders.Select(HevyApiMapping.ToDomain).ToImmutableList());
  }

  public async Task<RoutineFolder> GetRoutineFolderAsync(long folderId, CancellationToken cancellationToken) =>
      (await GetAsync(token => api.GetRoutineFolderAsync(folderId, apiKey, token), HevyJsonContext.Default.RoutineFolderResponse, cancellationToken)).ToDomain();

  public Task<ExerciseHistoryWindow> GetExerciseHistoryAsync(string exerciseTemplateId, int page, int pageSize, DateOnly? startDate, DateOnly? endDate, CancellationToken cancellationToken)
  {
    ValidatePagination(page, pageSize, 10);
    var offset = ExerciseHistoryQuery.PageOffset(page, pageSize);
    return GetExerciseHistoryWindowAsync(
        exerciseTemplateId,
        new ExerciseHistoryQuery(offset, pageSize, startDate, endDate),
        cancellationToken);
  }

  public async Task<ExerciseHistoryWindow> GetExerciseHistoryWindowAsync(
      string exerciseTemplateId,
      ExerciseHistoryQuery request,
      CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    request.Validate();
    HevyAuthenticationHandler.EnsureSafeTarget(httpClient.BaseAddress);

    try
    {
      using var response = await api.GetExerciseHistoryAsync(
          ValidateIdentifier(exerciseTemplateId, nameof(exerciseTemplateId)),
          request.StartDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
          request.EndDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
          apiKey,
          cancellationToken);
      HevyResponse.EnsureSuccess(response);
      await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
      return await ExerciseHistoryStreamReader.ReadAsync(
          stream,
          request,
          HevyJsonContext.Default.ExerciseHistoryEntryResponse,
          exerciseHistoryReadLimits.MaximumResponseBytes,
          response.StatusCode,
          cancellationToken);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (OperationCanceledException)
    {
      throw TimeoutException();
    }
    catch (HttpRequestException)
    {
      throw new HevyException("transient_upstream", "The Hevy API is temporarily unavailable.", true, null);
    }
    catch (ApiRequestException exception) when (exception.InnerException is OperationCanceledException && cancellationToken.IsCancellationRequested)
    {
      throw exception.InnerException;
    }
    catch (ApiRequestException exception) when (exception.InnerException is OperationCanceledException)
    {
      throw TimeoutException();
    }
    catch (ApiRequestException exception) when (exception.InnerException is HttpRequestException)
    {
      throw new HevyException("transient_upstream", "The Hevy API is temporarily unavailable.", true, null);
    }
  }

  public async Task<PagedResult<BodyMeasurement>> GetBodyMeasurementsAsync(int page, int pageSize, CancellationToken cancellationToken)
  {
    ValidatePagination(page, pageSize, 10);
    var response = await GetAsync(token => api.GetBodyMeasurementsAsync(page, pageSize, apiKey, token), HevyJsonContext.Default.BodyMeasurementPageResponse, cancellationToken);
    ValidatePage(response.Page, response.PageCount, response.BodyMeasurements, page, pageSize);
    return new PagedResult<BodyMeasurement>(response.Page, response.PageCount, response.BodyMeasurements.Select(HevyApiMapping.ToDomain).ToImmutableList());
  }

  public async Task<BodyMeasurement> GetBodyMeasurementAsync(DateOnly date, CancellationToken cancellationToken) =>
      (await GetAsync(token => api.GetBodyMeasurementAsync(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), apiKey, token), HevyJsonContext.Default.BodyMeasurementResponse, cancellationToken)).ToDomain();

  public async Task<Workout> CreateWorkoutAsync(CreateWorkoutCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);
    ValidateWorkout(command.Workout);
    var request = new CreateWorkoutRequest(command.Workout.ToRequest());
    return (await SendMutationAsync(HttpMethod.Post, "v1/workouts", request, HevyJsonContext.Default.CreateWorkoutRequest, HevyJsonContext.Default.WorkoutResponse, retrySafe: false, cancellationToken)).ToDomain();
  }

  public async Task<Workout> UpdateWorkoutAsync(string workoutId, UpdateWorkoutCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);
    ValidateWorkout(command.Workout);
    var request = new UpdateWorkoutRequest(command.Workout.ToRequest());
    return (await SendMutationAsync(HttpMethod.Put, $"v1/workouts/{EscapeIdentifier(workoutId, nameof(workoutId))}", request, HevyJsonContext.Default.UpdateWorkoutRequest, HevyJsonContext.Default.WorkoutResponse, retrySafe: false, cancellationToken)).ToDomain();
  }

  public async Task<Routine> CreateRoutineAsync(CreateRoutineCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);
    ValidateRoutine(command.Routine);
    var request = new CreateRoutineRequest(command.Routine.ToRequest());
    return (await SendMutationAsync(HttpMethod.Post, "v1/routines", request, HevyJsonContext.Default.CreateRoutineRequest, HevyJsonContext.Default.RoutineResponse, retrySafe: false, cancellationToken)).ToDomain();
  }

  public async Task<Routine> UpdateRoutineAsync(string routineId, UpdateRoutineCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);
    ValidateRoutine(command.Routine);
    var request = new UpdateRoutineRequest(command.Routine.ToRequest());
    return (await SendMutationAsync(HttpMethod.Put, $"v1/routines/{EscapeIdentifier(routineId, nameof(routineId))}", request, HevyJsonContext.Default.UpdateRoutineRequest, HevyJsonContext.Default.RoutineResponse, retrySafe: false, cancellationToken)).ToDomain();
  }

  public async Task<RoutineFolder> CreateRoutineFolderAsync(CreateRoutineFolderCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);
    ArgumentNullException.ThrowIfNull(command.RoutineFolder);
    ValidateRequiredText(command.RoutineFolder.Title, "routine folder title");
    var request = new CreateRoutineFolderRequest(command.RoutineFolder);
    return (await SendMutationAsync(HttpMethod.Post, "v1/routine_folders", request, HevyJsonContext.Default.CreateRoutineFolderRequest, HevyJsonContext.Default.RoutineFolderResponse, retrySafe: false, cancellationToken)).ToDomain();
  }

  public async Task<ExerciseTemplate> CreateExerciseTemplateAsync(CreateExerciseTemplateCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);
    ValidateExerciseTemplate(command.Exercise);
    var request = new CreateExerciseTemplateRequest(command.Exercise.ToRequest());
    var response = await SendMutationAsync(HttpMethod.Post, "v1/exercise_templates", request, HevyJsonContext.Default.CreateExerciseTemplateRequest, HevyJsonContext.Default.CreateExerciseTemplateResponse, retrySafe: false, cancellationToken);
    return await ReadCommittedResultAsync(
        () => GetExerciseTemplateAsync(response.Id.ToString(CultureInfo.InvariantCulture), cancellationToken),
        cancellationToken);
  }

  public async Task<BodyMeasurement> CreateBodyMeasurementAsync(NewBodyMeasurement measurement, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(measurement);
    ValidateMeasurementDate(measurement.Date, nameof(measurement));
    ValidateMeasurementValues(measurement.WeightKg, measurement.LeanMassKg, measurement.FatPercent, measurement.NeckCm, measurement.ShoulderCm, measurement.ChestCm, measurement.LeftBicepCm, measurement.RightBicepCm, measurement.LeftForearmCm, measurement.RightForearmCm, measurement.Abdomen, measurement.Waist, measurement.Hips, measurement.LeftThigh, measurement.RightThigh, measurement.LeftCalf, measurement.RightCalf);
    var request = measurement.ToRequest();
    await SendMutationWithoutResponseAsync(HttpMethod.Post, "v1/body_measurements", request, HevyJsonContext.Default.CreateBodyMeasurementRequest, retrySafe: false, cancellationToken);
    return await ReadCommittedResultAsync(() => GetBodyMeasurementAsync(measurement.Date, cancellationToken), cancellationToken);
  }

  public async Task<BodyMeasurement> UpdateBodyMeasurementAsync(DateOnly date, BodyMeasurementUpdate measurement, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(measurement);
    ValidateMeasurementDate(date, nameof(date));
    ValidateMeasurementValues(measurement.WeightKg, measurement.LeanMassKg, measurement.FatPercent, measurement.NeckCm, measurement.ShoulderCm, measurement.ChestCm, measurement.LeftBicepCm, measurement.RightBicepCm, measurement.LeftForearmCm, measurement.RightForearmCm, measurement.Abdomen, measurement.Waist, measurement.Hips, measurement.LeftThigh, measurement.RightThigh, measurement.LeftCalf, measurement.RightCalf);
    var request = measurement.ToRequest();
    await SendMutationWithoutResponseAsync(HttpMethod.Put, $"v1/body_measurements/{date:yyyy-MM-dd}", request, HevyJsonContext.Default.UpdateBodyMeasurementRequest, retrySafe: true, cancellationToken);
    return await ReadCommittedResultAsync(() => GetBodyMeasurementAsync(date, cancellationToken), cancellationToken);
  }

  private async Task<T> GetAsync<T>(Func<CancellationToken, Task<HttpResponseMessage>> send, JsonTypeInfo<T> jsonTypeInfo, CancellationToken cancellationToken)
  {
    HevyAuthenticationHandler.EnsureSafeTarget(httpClient.BaseAddress);
    try
    {
      using var response = await send(cancellationToken);
      return await HevyResponse.ReadAsync(response, jsonTypeInfo, cancellationToken);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (OperationCanceledException)
    {
      throw TimeoutException();
    }
    catch (HttpRequestException)
    {
      throw new HevyException("transient_upstream", "The Hevy API is temporarily unavailable.", true, null);
    }
    catch (ApiRequestException exception) when (exception.InnerException is OperationCanceledException && cancellationToken.IsCancellationRequested)
    {
      throw exception.InnerException;
    }
    catch (ApiRequestException exception) when (exception.InnerException is OperationCanceledException)
    {
      throw TimeoutException();
    }
    catch (ApiRequestException exception) when (exception.InnerException is HttpRequestException)
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
      if (response.IsSuccessStatusCode)
      {
        return await ReadCommittedResultAsync(
            () => HevyResponse.ReadAsync(response, responseTypeInfo, cancellationToken),
            cancellationToken);
      }
      return await HevyResponse.ReadAsync(response, responseTypeInfo, cancellationToken);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (OperationCanceledException)
    {
      throw new HevyOutcomeUnknownException();
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
    catch (OperationCanceledException)
    {
      throw new HevyOutcomeUnknownException();
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
      throw new HevyOutcomeUnknownException(response.StatusCode, HevyResponse.SafeRequestId(response));
    }
  }

  private void SetRetryDeadline(HttpRequestMessage request)
  {
    if (httpClient.Timeout != Timeout.InfiniteTimeSpan)
    {
      request.Options.Set(HevyRetryHandler.RetryDeadline, DateTimeOffset.UtcNow + httpClient.Timeout);
    }
  }

  private static async Task<T> ReadCommittedResultAsync<T>(Func<Task<T>> readBack, CancellationToken cancellationToken)
  {
    try
    {
      return await readBack().ConfigureAwait(false);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (Exception)
    {
      throw new HevyCommittedReadbackException();
    }
  }

  private static HevyException TimeoutException() =>
      new("timeout", "The Hevy API request timed out.", true, null);

  private static void ValidatePage<T>(int actualPage, int pageCount, ImmutableList<T>? items, int requestedPage, int requestedPageSize)
  {
    if (actualPage != requestedPage || pageCount < 0 || (pageCount == 0 && actualPage != 1) ||
        (pageCount > 0 && actualPage > pageCount) || items is null ||
        (pageCount == 0 && items.Count != 0) || (pageCount > 0 && items.Count == 0) ||
        items.Count > requestedPageSize)
    {
      throw HevyResponse.UnexpectedResponse(System.Net.HttpStatusCode.OK);
    }
  }

  private static void ValidateWorkout(CreateWorkoutWrite workout)
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

  private static void ValidateWorkout(UpdateWorkoutWrite workout)
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
      => Uri.EscapeDataString(ValidateIdentifier(value, parameterName));

  private static string ValidateIdentifier(string value, string parameterName)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      throw new ArgumentException("An identifier is required.", parameterName);
    }

    return value;
  }

  private static void ValidatePagination(int page, int pageSize, int maximumPageSize)
  {
    ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
    ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, maximumPageSize);
  }
}
