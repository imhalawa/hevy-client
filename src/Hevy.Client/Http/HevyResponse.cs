using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Hevy.Client.Errors;
using Hevy.Client.Models;

namespace Hevy.Client.Http;

internal static class HevyResponse
{
  internal const int MaximumResponseBytes = 4 * 1024 * 1024;

  public static void EnsureSuccess(HttpResponseMessage response)
  {
    ArgumentNullException.ThrowIfNull(response);
    if (!response.IsSuccessStatusCode)
    {
      throw CreateException(response.StatusCode);
    }
  }

  public static async Task<T> ReadAsync<T>(HttpResponseMessage response, JsonTypeInfo<T> jsonTypeInfo, CancellationToken cancellationToken)
  {
    EnsureSuccess(response);

    try
    {
      if (response.Content.Headers.ContentLength is > MaximumResponseBytes)
      {
        throw UnexpectedResponse(response.StatusCode);
      }

      await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
      using var payload = new MemoryStream();
      var buffer = new byte[81_920];
      while (true)
      {
        var remaining = MaximumResponseBytes + 1L - payload.Length;
        if (remaining <= 0) throw UnexpectedResponse(response.StatusCode);
        var read = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), cancellationToken);
        if (read == 0) break;
        payload.Write(buffer, 0, read);
      }

      if (payload.Length > MaximumResponseBytes) throw UnexpectedResponse(response.StatusCode);
      var value = JsonSerializer.Deserialize(payload.GetBuffer().AsSpan(0, checked((int)payload.Length)), jsonTypeInfo)
          ?? throw UnexpectedResponse(response.StatusCode);
      ValidateContract(value);
      return value;
    }
    catch (JsonException)
    {
      throw UnexpectedResponse(response.StatusCode);
    }
  }

  public static HevyException UnexpectedResponse(HttpStatusCode statusCode) =>
      new("unexpected_response", "The Hevy API returned an invalid response.", false, statusCode);

  internal static void ValidateContract(object value)
  {
    switch (value)
    {
      case UserInfoResponse response:
        Required(response.Data);
        ValidateContract(response.Data);
        break;
      case UserInfo user:
        RequiredText(user.Id);
        break;
      case WorkoutPage page:
        Required(page.Workouts);
        foreach (var workout in page.Workouts) ValidateContract(workout);
        break;
      case WorkoutEventsPage page:
        Required(page.Events);
        foreach (var item in page.Events) ValidateContract(item);
        break;
      case UpdatedWorkoutEvent updated:
        Required(updated.Workout);
        ValidateContract(updated.Workout);
        break;
      case DeletedWorkoutEvent deleted:
        RequiredText(deleted.Id);
        RequiredTimestamp(deleted.DeletedAt);
        break;
      case Workout workout:
        RequiredText(workout.Id);
        RequiredTimestamp(workout.StartTime);
        RequiredTimestamp(workout.EndTime);
        RequiredTimestamp(workout.UpdatedAt);
        RequiredTimestamp(workout.CreatedAt);
        Required(workout.Exercises);
        foreach (var exercise in workout.Exercises) ValidateContract(exercise);
        break;
      case WorkoutExercise exercise:
        RequiredText(exercise.ExerciseTemplateId);
        Required(exercise.Sets);
        break;
      case RoutinePage page:
        Required(page.Routines);
        foreach (var routine in page.Routines) ValidateContract(routine);
        break;
      case RoutineResponse response:
        Required(response.Routine);
        ValidateContract(response.Routine);
        break;
      case Routine routine:
        RequiredText(routine.Id);
        RequiredTimestamp(routine.UpdatedAt);
        RequiredTimestamp(routine.CreatedAt);
        Required(routine.Exercises);
        foreach (var exercise in routine.Exercises) ValidateContract(exercise);
        break;
      case RoutineExercise exercise:
        RequiredText(exercise.ExerciseTemplateId);
        Required(exercise.Sets);
        break;
      case RoutineFolderPage page:
        Required(page.RoutineFolders);
        foreach (var folder in page.RoutineFolders) ValidateContract(folder);
        break;
      case RoutineFolder folder:
        if (folder.Id <= 0) throw new JsonException();
        RequiredTimestamp(folder.UpdatedAt);
        RequiredTimestamp(folder.CreatedAt);
        break;
      case ExerciseTemplatePage page:
        Required(page.ExerciseTemplates);
        foreach (var template in page.ExerciseTemplates) ValidateContract(template);
        break;
      case ExerciseTemplate template:
        RequiredText(template.Id);
        Required(template.SecondaryMuscleGroups);
        break;
      case CreateExerciseTemplateResponse response when response.Id <= 0:
        throw new JsonException();
      case ExerciseHistoryEntry entry:
        RequiredText(entry.WorkoutId);
        RequiredText(entry.ExerciseTemplateId);
        RequiredTimestamp(entry.WorkoutStartTime);
        RequiredTimestamp(entry.WorkoutEndTime);
        break;
      case BodyMeasurementPage page:
        Required(page.BodyMeasurements);
        foreach (var measurement in page.BodyMeasurements) ValidateContract(measurement);
        break;
      case BodyMeasurement measurement when measurement.Date == default:
        throw new JsonException();
      case WorkoutCountResponse count when count.WorkoutCount < 0:
        throw new JsonException();
    }
  }

  private static void Required(object? value)
  {
    if (value is null) throw new JsonException();
  }

  private static void RequiredText(string? value)
  {
    if (string.IsNullOrWhiteSpace(value)) throw new JsonException();
  }

  private static void RequiredTimestamp(DateTimeOffset value)
  {
    if (value == default) throw new JsonException();
  }

  private static HevyException CreateException(HttpStatusCode statusCode) => statusCode switch
  {
    HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => new HevyException("validation", "The Hevy API rejected the request.", false, statusCode),
    HttpStatusCode.Unauthorized => new HevyException("authentication", "The Hevy API rejected the credentials.", false, statusCode),
    HttpStatusCode.Forbidden => new HevyException("authorization", "The Hevy API denied access to this resource.", false, statusCode),
    HttpStatusCode.NotFound => new HevyException("not_found", "The requested Hevy resource was not found.", false, statusCode),
    HttpStatusCode.Conflict => new HevyException("conflict", "The Hevy API reported a conflicting change.", false, statusCode),
    HttpStatusCode.TooManyRequests => new HevyException("rate_limited", "The Hevy API rate limit was reached.", true, statusCode),
    _ when (int)statusCode >= 500 => new HevyException("transient_upstream", "The Hevy API is temporarily unavailable.", true, statusCode),
    _ => new HevyException("unexpected_response", "The Hevy API returned an unexpected response.", false, statusCode),
  };
}
