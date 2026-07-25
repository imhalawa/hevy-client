using System.Globalization;
using System.Net.Http;
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

  internal static HevyAuthenticationHandler CreateProductionPipeline(HevyClientOptions options)
  {
    ArgumentNullException.ThrowIfNull(options);
    return new HevyAuthenticationHandler(options)
    {
      InnerHandler = new HttpClientHandler
      {
        AllowAutoRedirect = false,
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

  private async Task<T> GetAsync<T>(string relativeUri, JsonTypeInfo<T> jsonTypeInfo, CancellationToken cancellationToken)
  {
    var finalUri = httpClient.BaseAddress is null ? null : new Uri(httpClient.BaseAddress, relativeUri);
    HevyAuthenticationHandler.EnsureSafeTarget(finalUri);
    using var request = new HttpRequestMessage(HttpMethod.Get, relativeUri);
    request.Headers.TryAddWithoutValidation("api-key", apiKey);

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
