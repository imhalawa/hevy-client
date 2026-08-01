using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Hevy.Core.Exceptions;
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
      throw CreateException(response);
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
    catch (NotSupportedException)
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
      case UserInfoDataResponse user:
        RequiredText(user.Id);
        Required(user.Name);
        Required(user.Url);
        break;
      case WorkoutPageResponse page:
        Required(page.Workouts);
        foreach (var workout in page.Workouts)
        {
          Required(workout);
          ValidateContract(workout);
        }
        break;
      case WorkoutEventsPageResponse page:
        Required(page.Events);
        foreach (var item in page.Events)
        {
          Required(item);
          ValidateContract(item);
        }
        break;
      case UpdatedWorkoutEventResponse updated:
        Required(updated.Workout);
        ValidateContract(updated.Workout);
        break;
      case DeletedWorkoutEventResponse deleted:
        RequiredText(deleted.Id);
        RequiredTimestamp(deleted.DeletedAt);
        break;
      case WorkoutResponse workout:
        RequiredText(workout.Id);
        Required(workout.Title);
        Required(workout.RoutineId);
        Required(workout.Description);
        RequiredTimestamp(workout.StartTime);
        RequiredTimestamp(workout.EndTime);
        RequiredTimestamp(workout.UpdatedAt);
        RequiredTimestamp(workout.CreatedAt);
        Required(workout.Exercises);
        foreach (var exercise in workout.Exercises)
        {
          Required(exercise);
          ValidateContract(exercise);
        }
        break;
      case WorkoutExerciseResponse exercise:
        Required(exercise.Title);
        Required(exercise.Notes);
        RequiredText(exercise.ExerciseTemplateId);
        Required(exercise.Sets);
        foreach (var set in exercise.Sets)
        {
          Required(set);
          ValidateContract(set);
        }
        break;
      case WorkoutSetResponse set:
        Required(set.Type);
        break;
      case RoutinePageResponse page:
        Required(page.Routines);
        foreach (var routine in page.Routines)
        {
          Required(routine);
          ValidateContract(routine);
        }
        break;
      case RoutineEnvelopeResponse response:
        Required(response.Routine);
        ValidateContract(response.Routine);
        break;
      case RoutineResponse routine:
        RequiredText(routine.Id);
        Required(routine.Title);
        RequiredTimestamp(routine.UpdatedAt);
        RequiredTimestamp(routine.CreatedAt);
        Required(routine.Exercises);
        foreach (var exercise in routine.Exercises)
        {
          Required(exercise);
          ValidateContract(exercise);
        }
        break;
      case RoutineExerciseResponse exercise:
        Required(exercise.Title);
        Required(exercise.RestSeconds);
        Required(exercise.Notes);
        RequiredText(exercise.ExerciseTemplateId);
        Required(exercise.Sets);
        foreach (var set in exercise.Sets)
        {
          Required(set);
          ValidateContract(set);
        }
        break;
      case RoutineSetResponse set:
        Required(set.Type);
        break;
      case RoutineFolderPageResponse page:
        Required(page.RoutineFolders);
        foreach (var folder in page.RoutineFolders)
        {
          Required(folder);
          ValidateContract(folder);
        }
        break;
      case RoutineFolderResponse folder:
        if (folder.Id <= 0) throw new JsonException();
        Required(folder.Title);
        RequiredTimestamp(folder.UpdatedAt);
        RequiredTimestamp(folder.CreatedAt);
        break;
      case ExerciseTemplatePageResponse page:
        Required(page.ExerciseTemplates);
        foreach (var template in page.ExerciseTemplates)
        {
          Required(template);
          ValidateContract(template);
        }
        break;
      case ExerciseTemplateResponse template:
        RequiredText(template.Id);
        Required(template.Title);
        Required(template.Type);
        Required(template.PrimaryMuscleGroup);
        Required(template.SecondaryMuscleGroups);
        foreach (var muscle in template.SecondaryMuscleGroups) Required(muscle);
        break;
      case CreateExerciseTemplateResponse response when response.Id <= 0:
        throw new JsonException();
      case ExerciseHistoryEntryResponse entry:
        RequiredText(entry.WorkoutId);
        Required(entry.WorkoutTitle);
        RequiredText(entry.ExerciseTemplateId);
        RequiredTimestamp(entry.WorkoutStartTime);
        RequiredTimestamp(entry.WorkoutEndTime);
        Required(entry.SetType);
        break;
      case BodyMeasurementPageResponse page:
        Required(page.BodyMeasurements);
        foreach (var measurement in page.BodyMeasurements)
        {
          Required(measurement);
          ValidateContract(measurement);
        }
        break;
      case BodyMeasurementResponse measurement when measurement.Date == default:
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

  private static HevyException CreateException(HttpResponseMessage response)
  {
    var statusCode = response.StatusCode;
    var requestId = SafeRequestId(response);
    return statusCode switch
    {
      HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => new HevyException("validation", "The Hevy API rejected the request.", false, statusCode, requestId),
      HttpStatusCode.Unauthorized => new HevyException("authentication", "The Hevy API rejected the credentials.", false, statusCode, requestId),
      HttpStatusCode.Forbidden => new HevyException("authorization", "The Hevy API denied access to this resource.", false, statusCode, requestId),
      HttpStatusCode.NotFound => new HevyException("not_found", "The requested Hevy resource was not found.", false, statusCode, requestId),
      HttpStatusCode.Conflict => new HevyException("conflict", "The Hevy API reported a conflicting change.", false, statusCode, requestId),
      HttpStatusCode.TooManyRequests => new HevyException("rate_limited", "The Hevy API rate limit was reached.", true, statusCode, requestId),
      _ when (int)statusCode >= 500 => new HevyException("transient_upstream", "The Hevy API is temporarily unavailable.", true, statusCode, requestId),
      _ => new HevyException("unexpected_response", "The Hevy API returned an unexpected response.", false, statusCode, requestId),
    };
  }

  internal static string? SafeRequestId(HttpResponseMessage response)
  {
    if (!response.Headers.TryGetValues("X-Request-Id", out var values)) return null;
    using var enumerator = values.GetEnumerator();
    if (!enumerator.MoveNext()) return null;
    var value = enumerator.Current;
    if (enumerator.MoveNext()) return null;
    return value is { Length: >= 1 and <= 128 } && value.All(static character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or ':' or '-')
        ? value
        : null;
  }
}
