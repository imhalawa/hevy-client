using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Hevy.Core.Exceptions;
using Hevy.Client.Models;
using Hevy.Client.Http;
using Hevy.Client.Serialization;

namespace Hevy.Client;

public sealed class HevyClient : IHevyClient
{
  private readonly HttpClient httpClient;
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
    exerciseHistoryReadLimits.Validate();

    httpClient.BaseAddress = HevyAuthenticationHandler.ApiOrigin;
    httpClient.DefaultRequestHeaders.Remove("api-key");
    this.httpClient = httpClient;
    apiKey = options.ApiKey;
    this.exerciseHistoryReadLimits = exerciseHistoryReadLimits;
  }

  internal static HevyRetryHandler CreateProductionPipeline(HevyClientOptions options) =>
      new()
      {
        InnerHandler = new HevyAuthenticationHandler(options)
        {
          InnerHandler = new HttpClientHandler
          {
            AllowAutoRedirect = false,
          },
        },
      };

  public async Task<PagedResult<Workout>> GetWorkoutsAsync(int page, int pageSize, CancellationToken cancellationToken)
  {
    ValidatePagination(page, pageSize, 10);
    var response = await GetAsync($"v1/workouts?page={page}&pageSize={pageSize}", HevyJsonContext.Default.WorkoutPageResponse, cancellationToken);
    ValidatePage(response.Page, response.PageCount, response.Workouts, page, pageSize);
    return new PagedResult<Workout>(response.Page, response.PageCount, response.Workouts.Select(static workout => workout.ToDomain()).ToImmutableList());
  }

  public async Task<int> GetWorkoutCountAsync(CancellationToken cancellationToken) =>
      (await GetAsync("v1/workouts/count", HevyJsonContext.Default.WorkoutCountResponse, cancellationToken)).WorkoutCount;

  public async Task<PagedResult<WorkoutEvent>> GetWorkoutEventsAsync(int page, int pageSize, DateTimeOffset since, CancellationToken cancellationToken)
  {
    ValidatePagination(page, pageSize, 10);
    var timestamp = Uri.EscapeDataString(since.ToString("O", CultureInfo.InvariantCulture));
    var response = await GetAsync($"v1/workouts/events?page={page}&pageSize={pageSize}&since={timestamp}", HevyJsonContext.Default.WorkoutEventsPageResponse, cancellationToken);
    ValidatePage(response.Page, response.PageCount, response.Events, page, pageSize);
    return new PagedResult<WorkoutEvent>(response.Page, response.PageCount, response.Events.Select(static workoutEvent => workoutEvent.ToDomain()).ToImmutableList());
  }

  public async Task<Workout> GetWorkoutAsync(string workoutId, CancellationToken cancellationToken) =>
      (await GetAsync($"v1/workouts/{EscapeIdentifier(workoutId, nameof(workoutId))}", HevyJsonContext.Default.WorkoutResponse, cancellationToken)).ToDomain();

  public async Task<UserInfo> GetUserInfoAsync(CancellationToken cancellationToken) =>
      (await GetAsync("v1/user/info", HevyJsonContext.Default.UserInfoResponse, cancellationToken)).Data.ToDomain();

  public async Task<PagedResult<Routine>> GetRoutinesAsync(int page, int pageSize, CancellationToken cancellationToken)
  {
    ValidatePagination(page, pageSize, 10);
    var response = await GetAsync($"v1/routines?page={page}&pageSize={pageSize}", HevyJsonContext.Default.RoutinePageResponse, cancellationToken);
    ValidatePage(response.Page, response.PageCount, response.Routines, page, pageSize);
    return new PagedResult<Routine>(response.Page, response.PageCount, response.Routines.Select(static routine => routine.ToDomain()).ToImmutableList());
  }

  public async Task<Routine> GetRoutineAsync(string routineId, CancellationToken cancellationToken) =>
      (await GetAsync($"v1/routines/{EscapeIdentifier(routineId, nameof(routineId))}", HevyJsonContext.Default.RoutineEnvelopeResponse, cancellationToken)).Routine.ToDomain();

  public async Task<PagedResult<ExerciseTemplate>> GetExerciseTemplatesAsync(int page, int pageSize, CancellationToken cancellationToken)
  {
    ValidatePagination(page, pageSize, 100);
    var response = await GetAsync($"v1/exercise_templates?page={page}&pageSize={pageSize}", HevyJsonContext.Default.ExerciseTemplatePageResponse, cancellationToken);
    ValidatePage(response.Page, response.PageCount, response.ExerciseTemplates, page, pageSize);
    return new PagedResult<ExerciseTemplate>(response.Page, response.PageCount, response.ExerciseTemplates.Select(static exercise => exercise.ToDomain()).ToImmutableList());
  }

  public async Task<ExerciseTemplate> GetExerciseTemplateAsync(string exerciseTemplateId, CancellationToken cancellationToken) =>
      (await GetAsync($"v1/exercise_templates/{EscapeIdentifier(exerciseTemplateId, nameof(exerciseTemplateId))}", HevyJsonContext.Default.ExerciseTemplateResponse, cancellationToken)).ToDomain();

  public async Task<PagedResult<RoutineFolder>> GetRoutineFoldersAsync(int page, int pageSize, CancellationToken cancellationToken)
  {
    ValidatePagination(page, pageSize, 10);
    var response = await GetAsync($"v1/routine_folders?page={page}&pageSize={pageSize}", HevyJsonContext.Default.RoutineFolderPageResponse, cancellationToken);
    ValidatePage(response.Page, response.PageCount, response.RoutineFolders, page, pageSize);
    return new PagedResult<RoutineFolder>(response.Page, response.PageCount, response.RoutineFolders.Select(static folder => folder.ToDomain()).ToImmutableList());
  }

  public async Task<RoutineFolder> GetRoutineFolderAsync(long folderId, CancellationToken cancellationToken) =>
      (await GetAsync($"v1/routine_folders/{folderId}", HevyJsonContext.Default.RoutineFolderResponse, cancellationToken)).ToDomain();

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
    request.Validate();
    var query = HistoryQuery(request.StartDate, request.EndDate);
    return await GetAsync(
        $"v1/exercise_history/{EscapeIdentifier(exerciseTemplateId, nameof(exerciseTemplateId))}{query}",
        async (response, token) =>
    {
      if (!response.IsSuccessStatusCode) throw HevyResponse.CreateException(response);
      await using var stream = await response.Content.ReadAsStreamAsync(token);
      return await ExerciseHistoryStreamReader.ReadAsync(
          stream,
          request,
          HevyJsonContext.Default.ExerciseHistoryEntryResponse,
          exerciseHistoryReadLimits.MaximumResponseBytes,
          response.StatusCode,
          token);
    },
        cancellationToken);
  }

  public async Task<PagedResult<BodyMeasurement>> GetBodyMeasurementsAsync(int page, int pageSize, CancellationToken cancellationToken)
  {
    ValidatePagination(page, pageSize, 10);
    var response = await GetAsync($"v1/body_measurements?page={page}&pageSize={pageSize}", HevyJsonContext.Default.BodyMeasurementPageResponse, cancellationToken);
    ValidatePage(response.Page, response.PageCount, response.BodyMeasurements, page, pageSize);
    return new PagedResult<BodyMeasurement>(response.Page, response.PageCount, response.BodyMeasurements.Select(static measurement => measurement.ToDomain()).ToImmutableList());
  }

  public async Task<BodyMeasurement> GetBodyMeasurementAsync(DateOnly date, CancellationToken cancellationToken) =>
      (await GetAsync($"v1/body_measurements/{date:yyyy-MM-dd}", HevyJsonContext.Default.BodyMeasurementResponse, cancellationToken)).ToDomain();

  public async Task<Workout> CreateWorkoutAsync(CreateWorkoutCommand command, CancellationToken cancellationToken)
  {
    CreateWorkoutRequest request = command;
    return (await SendMutationAsync(HttpMethod.Post, "v1/workouts", request, HevyJsonContext.Default.CreateWorkoutRequest, HevyJsonContext.Default.WorkoutResponse, retrySafe: false, cancellationToken)).ToDomain();
  }

  public async Task<Workout> UpdateWorkoutAsync(string workoutId, UpdateWorkoutCommand command, CancellationToken cancellationToken)
  {
    UpdateWorkoutRequest request = command;
    return (await SendMutationAsync(HttpMethod.Put, $"v1/workouts/{EscapeIdentifier(workoutId, nameof(workoutId))}", request, HevyJsonContext.Default.UpdateWorkoutRequest, HevyJsonContext.Default.WorkoutResponse, retrySafe: false, cancellationToken)).ToDomain();
  }

  public async Task<Routine> CreateRoutineAsync(CreateRoutineCommand command, CancellationToken cancellationToken)
  {
    CreateRoutineRequest request = command;
    return (await SendMutationAsync(HttpMethod.Post, "v1/routines", request, HevyJsonContext.Default.CreateRoutineRequest, HevyJsonContext.Default.RoutineResponse, retrySafe: false, cancellationToken)).ToDomain();
  }

  public async Task<Routine> UpdateRoutineAsync(string routineId, UpdateRoutineCommand command, CancellationToken cancellationToken)
  {
    UpdateRoutineRequest request = command;
    return (await SendMutationAsync(HttpMethod.Put, $"v1/routines/{EscapeIdentifier(routineId, nameof(routineId))}", request, HevyJsonContext.Default.UpdateRoutineRequest, HevyJsonContext.Default.RoutineResponse, retrySafe: false, cancellationToken)).ToDomain();
  }

  public async Task<RoutineFolder> CreateRoutineFolderAsync(CreateRoutineFolderCommand command, CancellationToken cancellationToken)
  {
    CreateRoutineFolderRequest request = command;
    return (await SendMutationAsync(HttpMethod.Post, "v1/routine_folders", request, HevyJsonContext.Default.CreateRoutineFolderRequest, HevyJsonContext.Default.RoutineFolderResponse, retrySafe: false, cancellationToken)).ToDomain();
  }

  public async Task<ExerciseTemplate> CreateExerciseTemplateAsync(CreateExerciseTemplateCommand command, CancellationToken cancellationToken)
  {
    CreateExerciseTemplateRequest request = command;
    var response = await SendMutationAsync(HttpMethod.Post, "v1/exercise_templates", request, HevyJsonContext.Default.CreateExerciseTemplateRequest, HevyJsonContext.Default.CreateExerciseTemplateResponse, retrySafe: false, cancellationToken);
    return await ReadCommittedResultAsync(
        () => GetExerciseTemplateAsync(response.Id.ToString(CultureInfo.InvariantCulture), cancellationToken),
        cancellationToken);
  }

  public async Task<BodyMeasurement> CreateBodyMeasurementAsync(CreateBodyMeasurementCommand command, CancellationToken cancellationToken)
  {
    CreateBodyMeasurementRequest request = command;
    await SendMutationWithoutResponseAsync(HttpMethod.Post, "v1/body_measurements", request, HevyJsonContext.Default.CreateBodyMeasurementRequest, retrySafe: false, cancellationToken);
    return await ReadCommittedResultAsync(() => GetBodyMeasurementAsync(command.Measurement.Date, cancellationToken), cancellationToken);
  }

  public async Task<BodyMeasurement> UpdateBodyMeasurementAsync(DateOnly date, UpdateBodyMeasurementCommand command, CancellationToken cancellationToken)
  {
    UpdateBodyMeasurementRequest request = command;
    await SendMutationWithoutResponseAsync(HttpMethod.Put, $"v1/body_measurements/{date:yyyy-MM-dd}", request, HevyJsonContext.Default.UpdateBodyMeasurementRequest, retrySafe: true, cancellationToken);
    return await ReadCommittedResultAsync(() => GetBodyMeasurementAsync(date, cancellationToken), cancellationToken);
  }

  private Task<T> GetAsync<T>(string relativeUri, JsonTypeInfo<T> jsonTypeInfo, CancellationToken cancellationToken) =>
      GetAsync(relativeUri, (response, token) => HevyResponse.ReadAsync(response, jsonTypeInfo, token), cancellationToken);

  private async Task<T> GetAsync<T>(
      string relativeUri,
      Func<HttpResponseMessage, CancellationToken, Task<T>> readResponse,
      CancellationToken cancellationToken)
  {
    HevyAuthenticationHandler.EnsureSafeTarget(httpClient.BaseAddress);
    try
    {
      using var response = await SendGetAsync(relativeUri, cancellationToken);
      return await readResponse(response, cancellationToken);
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
  }

  private async Task<HttpResponseMessage> SendGetAsync(string relativeUri, CancellationToken cancellationToken)
  {
    using var request = new HttpRequestMessage(HttpMethod.Get, relativeUri);
    request.Headers.TryAddWithoutValidation("api-key", apiKey);
    SetRetryDeadline(request);
    return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
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
      if (!response.IsSuccessStatusCode) throw HevyResponse.CreateException(response);
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
    var request = new HttpRequestMessage(method, relativeUri);
    request.Headers.TryAddWithoutValidation("api-key", apiKey);
    request.Content = JsonContent.Create(requestBody, requestTypeInfo);
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
    var matchesRequest = actualPage == requestedPage;
    var hasValidBounds = pageCount >= 0 &&
        (pageCount == 0 ? actualPage == 1 : actualPage <= pageCount);
    var hasValidItems = items is not null && items.Count <= requestedPageSize &&
        (pageCount == 0 ? items.Count == 0 : items.Count > 0);

    if (!matchesRequest || !hasValidBounds || !hasValidItems)
    {
      throw HevyResponse.UnexpectedResponse(System.Net.HttpStatusCode.OK);
    }
  }

  private static string EscapeIdentifier(string value, string parameterName)
      => Uri.EscapeDataString(ValidateIdentifier(value, parameterName));

  private static string HistoryQuery(DateOnly? startDate, DateOnly? endDate)
  {
    var parameters = new List<string>(2);
    if (startDate is not null) parameters.Add($"start_date={startDate:yyyy-MM-dd}");
    if (endDate is not null) parameters.Add($"end_date={endDate:yyyy-MM-dd}");
    return parameters.Count == 0 ? string.Empty : $"?{string.Join('&', parameters)}";
  }

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
