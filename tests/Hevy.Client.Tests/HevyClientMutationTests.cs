using System.Net;
using Hevy.Client;
using Hevy.Client.Errors;
using Hevy.Client.Http;
using Hevy.Client.Models;
using TestSupport;
using Xunit;

namespace Hevy.Client.Tests;

public sealed class HevyClientMutationTests
{
  public static TheoryData<string, string> DirectMutationResponseFailures
  {
    get
    {
      var cases = new TheoryData<string, string>();
      foreach (var operation in new[] { "create_workout", "update_workout", "create_routine", "update_routine", "create_folder", "create_template" })
      {
        cases.Add(operation, "malformed");
        cases.Add(operation, "missing_required");
        cases.Add(operation, "oversized");
      }
      return cases;
    }
  }

  [Fact]
  public async Task Mutation_methods_reject_invalid_bodies_before_sending_a_request()
  {
    var handler = RespondingWith(Fixture.Read("workout.json"));
    var client = CreateClient(handler);

    await FluentActions.Awaiting(() =>
        client.CreateWorkoutAsync(null!, CancellationToken.None)).Should().ThrowExactlyAsync<ArgumentNullException>();

    var requestWithNullSet = new CreateWorkoutRequest(
        new WorkoutWrite(
            "Valid title",
            null,
            new DateTimeOffset(2024, 8, 14, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2024, 8, 14, 12, 30, 0, 0, TimeSpan.Zero),
            false,
            [new WorkoutExerciseWrite("D04AC939", null, null, [null!])]));

    await FluentActions.Awaiting(() =>
        client.CreateWorkoutAsync(requestWithNullSet, CancellationToken.None)).Should().ThrowExactlyAsync<ArgumentNullException>();

    (handler.Requests).Should().BeEmpty();
  }

  [Fact]
  public async Task Mutation_methods_send_documented_verbs_paths_and_bodies()
  {
    var responses = new Queue<HttpResponseMessage>([
        RecordingHttpMessageHandler.Json(HttpStatusCode.Created, Fixture.Read("workout.json")),
        RecordingHttpMessageHandler.Json(HttpStatusCode.OK, Fixture.Read("workout.json")),
        RecordingHttpMessageHandler.Json(HttpStatusCode.Created, Fixture.Read("routine.json")),
        RecordingHttpMessageHandler.Json(HttpStatusCode.OK, Fixture.Read("routine.json")),
        RecordingHttpMessageHandler.Json(HttpStatusCode.Created, Fixture.Read("routine-folder.json")),
        RecordingHttpMessageHandler.Json(HttpStatusCode.OK, Fixture.Read("exercise-template-create.json")),
        RecordingHttpMessageHandler.Json(HttpStatusCode.OK, Fixture.Read("exercise-template.json")),
        RecordingHttpMessageHandler.Json(HttpStatusCode.OK, "{}"),
        RecordingHttpMessageHandler.Json(HttpStatusCode.OK, Fixture.Read("body-measurement.json")),
        RecordingHttpMessageHandler.Json(HttpStatusCode.OK, "{}"),
        RecordingHttpMessageHandler.Json(HttpStatusCode.OK, Fixture.Read("body-measurement.json"))]);
    var handler = new RecordingHttpMessageHandler((_, _) => responses.Dequeue());
    var client = CreateClient(handler);

    await client.CreateWorkoutAsync(FixtureFactory.CreateWorkoutRequest(), CancellationToken.None);
    await client.UpdateWorkoutAsync("workout/a", FixtureFactory.UpdateWorkoutRequest(), CancellationToken.None);
    await client.CreateRoutineAsync(FixtureFactory.CreateRoutineRequest(), CancellationToken.None);
    ((await client.UpdateRoutineAsync("routine/a", FixtureFactory.UpdateRoutineRequest(), CancellationToken.None)).Id).Should().Be("routine-1");
    await client.CreateRoutineFolderAsync(FixtureFactory.CreateRoutineFolderRequest(), CancellationToken.None);
    await client.CreateExerciseTemplateAsync(FixtureFactory.CreateExerciseTemplateRequest(), CancellationToken.None);
    await client.CreateBodyMeasurementAsync(FixtureFactory.CreateBodyMeasurementRequest(), CancellationToken.None);
    await client.UpdateBodyMeasurementAsync(new DateOnly(2024, 8, 14), FixtureFactory.UpdateBodyMeasurementRequest(), CancellationToken.None);

    (handler.Requests.Select(request => (request.Method, request.RequestUri!.AbsoluteUri, request.Body))).Should().Equal([
        (HttpMethod.Post, "https://api.hevyapp.com/v1/workouts", "{\"workout\":{\"title\":\"Friday Leg Day\",\"description\":\"Sanitized workout\",\"start_time\":\"2024-08-14T12:00:00+00:00\",\"end_time\":\"2024-08-14T12:30:00+00:00\",\"is_private\":false,\"exercises\":[{\"exercise_template_id\":\"D04AC939\",\"superset_id\":null,\"notes\":\"Sanitized note\",\"sets\":[{\"type\":\"normal\",\"weight_kg\":100,\"reps\":10,\"distance_meters\":null,\"duration_seconds\":null,\"custom_metric\":null,\"rpe\":8.5}]}]}}"),
        (HttpMethod.Put, "https://api.hevyapp.com/v1/workouts/workout%2Fa", "{\"workout\":{\"title\":\"Friday Leg Day\",\"description\":\"Sanitized workout\",\"start_time\":\"2024-08-14T12:00:00+00:00\",\"end_time\":\"2024-08-14T12:30:00+00:00\",\"is_private\":false,\"exercises\":[{\"exercise_template_id\":\"D04AC939\",\"superset_id\":null,\"notes\":\"Sanitized note\",\"sets\":[{\"type\":\"normal\",\"weight_kg\":100,\"reps\":10,\"distance_meters\":null,\"duration_seconds\":null,\"custom_metric\":null,\"rpe\":8.5}]}]}}"),
        (HttpMethod.Post, "https://api.hevyapp.com/v1/routines", "{\"routine\":{\"title\":\"April Leg Day\",\"folder_id\":null,\"notes\":\"Sanitized routine\",\"exercises\":[{\"exercise_template_id\":\"D04AC939\",\"superset_id\":null,\"rest_seconds\":90,\"notes\":\"Controlled\",\"sets\":[{\"type\":\"normal\",\"weight_kg\":100,\"reps\":10,\"distance_meters\":null,\"duration_seconds\":null,\"custom_metric\":null,\"rep_range\":{\"start\":8,\"end\":12}}]}]}}"),
        (HttpMethod.Put, "https://api.hevyapp.com/v1/routines/routine%2Fa", "{\"routine\":{\"title\":\"April Leg Day\",\"notes\":\"Sanitized routine\",\"exercises\":[{\"exercise_template_id\":\"D04AC939\",\"superset_id\":null,\"rest_seconds\":90,\"notes\":\"Controlled\",\"sets\":[{\"type\":\"normal\",\"weight_kg\":100,\"reps\":10,\"distance_meters\":null,\"duration_seconds\":null,\"custom_metric\":null,\"rep_range\":{\"start\":8,\"end\":12}}]}]}}"),
        (HttpMethod.Post, "https://api.hevyapp.com/v1/routine_folders", "{\"routine_folder\":{\"title\":\"Push Pull\"}}"),
        (HttpMethod.Post, "https://api.hevyapp.com/v1/exercise_templates", "{\"exercise\":{\"title\":\"Bench Press\",\"exercise_type\":\"weight_reps\",\"equipment_category\":\"barbell\",\"muscle_group\":\"chest\",\"other_muscles\":[\"triceps\",\"shoulders\"]}}"),
        (HttpMethod.Get, "https://api.hevyapp.com/v1/exercise_templates/123", (string?)null),
        (HttpMethod.Post, "https://api.hevyapp.com/v1/body_measurements", "{\"date\":\"2024-08-14\",\"weight_kg\":80.5,\"lean_mass_kg\":65,\"fat_percent\":18.5,\"neck_cm\":38,\"shoulder_cm\":115,\"chest_cm\":95,\"left_bicep_cm\":35,\"right_bicep_cm\":35.5,\"left_forearm_cm\":28,\"right_forearm_cm\":28.5,\"abdomen\":85,\"waist\":80,\"hips\":95,\"left_thigh\":55,\"right_thigh\":55.5,\"left_calf\":37,\"right_calf\":37.5}"),
        (HttpMethod.Get, "https://api.hevyapp.com/v1/body_measurements/2024-08-14", (string?)null),
        (HttpMethod.Put, "https://api.hevyapp.com/v1/body_measurements/2024-08-14", "{\"weight_kg\":80.5,\"lean_mass_kg\":65,\"fat_percent\":18.5,\"neck_cm\":38,\"shoulder_cm\":115,\"chest_cm\":95,\"left_bicep_cm\":35,\"right_bicep_cm\":35.5,\"left_forearm_cm\":28,\"right_forearm_cm\":28.5,\"abdomen\":85,\"waist\":80,\"hips\":95,\"left_thigh\":55,\"right_thigh\":55.5,\"left_calf\":37,\"right_calf\":37.5}"),
        (HttpMethod.Get, "https://api.hevyapp.com/v1/body_measurements/2024-08-14", (string?)null),
    ]);
  }

  [Fact]
  public async Task Create_workout_propagates_cancellation_to_the_transport()
  {
    var handler = new RecordingHttpMessageHandler((_, cancellationToken) =>
    {
      cancellationToken.ThrowIfCancellationRequested();
      return RecordingHttpMessageHandler.Json(HttpStatusCode.Created, Fixture.Read("workout.json"));
    });
    var client = CreateClient(handler);
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();

    await FluentActions.Awaiting(() =>
        client.CreateWorkoutAsync(FixtureFactory.CreateWorkoutRequest(), cancellation.Token)).Should().ThrowAsync<OperationCanceledException>();

    ((handler.Requests).Should().ContainSingle().Which.CancellationToken.IsCancellationRequested).Should().BeTrue();
  }

  [Fact]
  public async Task Http_client_timeout_after_mutation_send_is_outcome_unknown()
  {
    using var httpClient = new HttpClient(new DelayingHandler()) { Timeout = TimeSpan.FromMilliseconds(20) };
    var client = new HevyClient(httpClient, new HevyClientOptions("test-api-key"));

    var exception = (await FluentActions.Awaiting(() =>
        client.CreateWorkoutAsync(FixtureFactory.CreateWorkoutRequest(), CancellationToken.None)).Should().ThrowExactlyAsync<HevyOutcomeUnknownException>()).Which;

    (exception.Code).Should().Be("outcome_unknown");
  }

  [Fact]
  public async Task Committed_custom_exercise_with_failed_readback_is_non_retryable_and_forbids_replay()
  {
    var responses = new Queue<HttpResponseMessage>([
        RecordingHttpMessageHandler.Json(HttpStatusCode.Created, Fixture.Read("exercise-template-create.json")),
        RecordingHttpMessageHandler.Json(HttpStatusCode.ServiceUnavailable, "{}")]);
    var client = CreateClient(new RecordingHttpMessageHandler((_, _) => responses.Dequeue()));

    var exception = (await FluentActions.Awaiting(() =>
        client.CreateExerciseTemplateAsync(FixtureFactory.CreateExerciseTemplateRequest(), CancellationToken.None)).Should().ThrowAsync<Exception>()).Which;

    (exception.GetType().GetProperty("Code")?.GetValue(exception)).Should().Be("committed_readback_failed");
    (exception.GetType().GetProperty("IsRetryable")?.GetValue(exception)).Should().Be(false);
    (exception.Message).Should().ContainEquivalentOf("fetch");
    (exception.Message).Should().ContainEquivalentOf("do not replay");
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task Committed_measurement_with_failed_readback_is_non_retryable_and_forbids_replay(bool update)
  {
    var responses = new Queue<HttpResponseMessage>([
        RecordingHttpMessageHandler.Json(HttpStatusCode.OK, "{}"),
        RecordingHttpMessageHandler.Json(HttpStatusCode.ServiceUnavailable, "{}")]);
    var client = CreateClient(new RecordingHttpMessageHandler((_, _) => responses.Dequeue()));

    var exception = update
        ? (await FluentActions.Awaiting(() => client.UpdateBodyMeasurementAsync(new DateOnly(2024, 8, 14), FixtureFactory.UpdateBodyMeasurementRequest(), CancellationToken.None)).Should().ThrowAsync<Exception>()).Which
        : (await FluentActions.Awaiting(() => client.CreateBodyMeasurementAsync(FixtureFactory.CreateBodyMeasurementRequest(), CancellationToken.None)).Should().ThrowAsync<Exception>()).Which;

    (exception.GetType().GetProperty("Code")?.GetValue(exception)).Should().Be("committed_readback_failed");
    (exception.GetType().GetProperty("IsRetryable")?.GetValue(exception)).Should().Be(false);
    (exception.Message).Should().ContainEquivalentOf("fetch");
    (exception.Message).Should().ContainEquivalentOf("do not replay");
  }

  [Theory]
  [MemberData(nameof(DirectMutationResponseFailures))]
  public async Task Every_direct_response_mutation_reports_post_commit_body_failures_as_non_replayable(string operation, string failure)
  {
    var validResponse = operation switch
    {
      "create_workout" or "update_workout" => Fixture.Read("workout.json"),
      "create_routine" or "update_routine" => Fixture.Read("routine.json"),
      "create_folder" => Fixture.Read("routine-folder.json"),
      "create_template" => Fixture.Read("exercise-template-create.json"),
      _ => throw new ArgumentOutOfRangeException(nameof(operation)),
    };
    var response = failure switch
    {
      "malformed" => "{",
      "missing_required" => "{}",
      "oversized" => validResponse[..^1] + ",\"future\":\"" + new string('x', 4_194_304) + "\"}",
      _ => throw new ArgumentOutOfRangeException(nameof(failure)),
    };
    var client = CreateClient(new RecordingHttpMessageHandler((_, _) =>
        RecordingHttpMessageHandler.Json(HttpStatusCode.Created, response)));

    var exception = (await FluentActions.Awaiting(() => InvokeDirectMutationAsync(client, operation)).Should().ThrowExactlyAsync<HevyCommittedReadbackException>()).Which;

    (exception.Code).Should().Be("committed_readback_failed");
    (exception.IsRetryable).Should().BeFalse();
    (exception.Message).Should().ContainEquivalentOf("do not replay");
  }

  [Theory]
  [InlineData("create_template")]
  [InlineData("create_measurement")]
  [InlineData("update_measurement")]
  public async Task Follow_up_readback_preserves_genuine_caller_cancellation_after_the_write_is_committed(string operation)
  {
    using var cancellation = new CancellationTokenSource();
    var responses = 0;
    var handler = new RecordingHttpMessageHandler((_, cancellationToken) =>
    {
      if (responses++ == 0)
      {
        var body = operation == "create_template" ? Fixture.Read("exercise-template-create.json") : "{}";
        return RecordingHttpMessageHandler.Json(HttpStatusCode.Created, body);
      }

      cancellation.Cancel();
      cancellationToken.ThrowIfCancellationRequested();
      throw new InvalidOperationException("Unreachable.");
    });
    var client = CreateClient(handler);

    await FluentActions.Awaiting(() => InvokeFollowUpMutationAsync(client, operation, cancellation.Token)).Should().ThrowAsync<OperationCanceledException>();

    (handler.Requests.Count).Should().Be(2);
  }

  [Fact]
  public async Task Mutation_transport_failures_are_unknown_without_a_retry_handler()
  {
    var handler = new RecordingHttpMessageHandler((_, _) => throw new HttpRequestException("transient"));
    var client = CreateClient(handler);

    await FluentActions.Awaiting(() =>
        client.CreateWorkoutAsync(FixtureFactory.CreateWorkoutRequest(), CancellationToken.None)).Should().ThrowExactlyAsync<HevyOutcomeUnknownException>();
    await FluentActions.Awaiting(() =>
        client.CreateBodyMeasurementAsync(FixtureFactory.CreateBodyMeasurementRequest(), CancellationToken.None)).Should().ThrowExactlyAsync<HevyOutcomeUnknownException>();

    (handler.Requests.Count).Should().Be(2);
  }

  [Fact]
  public async Task Mutation_5xx_responses_are_unknown_without_a_retry_handler()
  {
    var handler = new RecordingHttpMessageHandler((_, _) => RecordingHttpMessageHandler.Json(HttpStatusCode.NotImplemented, "{}"));
    var client = CreateClient(handler);

    var workoutException = (await FluentActions.Awaiting(() =>
        client.CreateWorkoutAsync(FixtureFactory.CreateWorkoutRequest(), CancellationToken.None)).Should().ThrowExactlyAsync<HevyOutcomeUnknownException>()).Which;
    var measurementException = (await FluentActions.Awaiting(() =>
        client.CreateBodyMeasurementAsync(FixtureFactory.CreateBodyMeasurementRequest(), CancellationToken.None)).Should().ThrowExactlyAsync<HevyOutcomeUnknownException>()).Which;

    (workoutException.StatusCode).Should().Be(HttpStatusCode.NotImplemented);
    (measurementException.StatusCode).Should().Be(HttpStatusCode.NotImplemented);
    (handler.Requests.Count).Should().Be(2);
  }

  [Fact]
  public async Task Mutation_5xx_response_preserves_a_single_safe_request_identifier_without_a_retry_handler()
  {
    var handler = new RecordingHttpMessageHandler((_, _) =>
    {
      var response = RecordingHttpMessageHandler.Json(HttpStatusCode.NotImplemented, "{}");
      response.Headers.TryAddWithoutValidation("X-Request-Id", "safe-request-id");
      return response;
    });
    var client = CreateClient(handler);

    var exception = (await FluentActions.Awaiting(() =>
        client.CreateWorkoutAsync(FixtureFactory.CreateWorkoutRequest(), CancellationToken.None)).Should().ThrowExactlyAsync<HevyOutcomeUnknownException>()).Which;

    (exception.RequestId).Should().Be("safe-request-id");
  }

  [Fact]
  public async Task Mutation_5xx_response_rejects_ambiguous_request_identifiers_without_changing_the_error_category()
  {
    var handler = new RecordingHttpMessageHandler((_, _) =>
    {
      var response = RecordingHttpMessageHandler.Json(HttpStatusCode.NotImplemented, "{}");
      response.Headers.TryAddWithoutValidation("X-Request-Id", ["first-request-id", "second-request-id"]);
      return response;
    });
    var client = CreateClient(handler);

    var exception = (await FluentActions.Awaiting(() =>
        client.CreateWorkoutAsync(FixtureFactory.CreateWorkoutRequest(), CancellationToken.None)).Should().ThrowExactlyAsync<HevyOutcomeUnknownException>()).Which;

    (exception.RequestId).Should().BeNull();
  }

  [Fact]
  public async Task Only_the_documented_full_replacement_measurement_update_is_retried()
  {
    var unsafeHandler = new RecordingHttpMessageHandler((_, _) => RecordingHttpMessageHandler.Json(HttpStatusCode.ServiceUnavailable, "{}"));
    var unsafeClient = CreateRetryingClient(unsafeHandler);

    await FluentActions.Awaiting(() =>
        unsafeClient.UpdateWorkoutAsync("workout-1", FixtureFactory.UpdateWorkoutRequest(), CancellationToken.None)).Should().ThrowExactlyAsync<HevyOutcomeUnknownException>();

    (unsafeHandler.Requests).Should().ContainSingle();

    var responses = new Queue<HttpResponseMessage>([
        RecordingHttpMessageHandler.Json(HttpStatusCode.ServiceUnavailable, "{}"),
        RecordingHttpMessageHandler.Json(HttpStatusCode.OK, "{}"),
        RecordingHttpMessageHandler.Json(HttpStatusCode.OK, Fixture.Read("body-measurement.json"))]);
    var safeHandler = new RecordingHttpMessageHandler((_, _) => responses.Dequeue());
    var safeClient = CreateRetryingClient(safeHandler);

    var measurement = await safeClient.UpdateBodyMeasurementAsync(new DateOnly(2024, 8, 14), FixtureFactory.UpdateBodyMeasurementRequest(), CancellationToken.None);

    (measurement.Date).Should().Be(new DateOnly(2024, 8, 14));
    (safeHandler.Requests.Count).Should().Be(3);
  }

  private static HevyClient CreateClient(RecordingHttpMessageHandler handler) =>
      new(new HttpClient(handler), new HevyClientOptions("test-api-key"));

  private static Task InvokeDirectMutationAsync(HevyClient client, string operation) => operation switch
  {
    "create_workout" => client.CreateWorkoutAsync(FixtureFactory.CreateWorkoutRequest(), default),
    "update_workout" => client.UpdateWorkoutAsync("workout-1", FixtureFactory.UpdateWorkoutRequest(), default),
    "create_routine" => client.CreateRoutineAsync(FixtureFactory.CreateRoutineRequest(), default),
    "update_routine" => client.UpdateRoutineAsync("routine-1", FixtureFactory.UpdateRoutineRequest(), default),
    "create_folder" => client.CreateRoutineFolderAsync(FixtureFactory.CreateRoutineFolderRequest(), default),
    "create_template" => client.CreateExerciseTemplateAsync(FixtureFactory.CreateExerciseTemplateRequest(), default),
    _ => throw new ArgumentOutOfRangeException(nameof(operation)),
  };

  private static Task InvokeFollowUpMutationAsync(HevyClient client, string operation, CancellationToken cancellationToken) => operation switch
  {
    "create_template" => client.CreateExerciseTemplateAsync(FixtureFactory.CreateExerciseTemplateRequest(), cancellationToken),
    "create_measurement" => client.CreateBodyMeasurementAsync(FixtureFactory.CreateBodyMeasurementRequest(), cancellationToken),
    "update_measurement" => client.UpdateBodyMeasurementAsync(new DateOnly(2024, 8, 14), FixtureFactory.UpdateBodyMeasurementRequest(), cancellationToken),
    _ => throw new ArgumentOutOfRangeException(nameof(operation)),
  };

  private static RecordingHttpMessageHandler RespondingWith(string response) =>
      new((_, _) => RecordingHttpMessageHandler.Json(HttpStatusCode.OK, response));

  private static HevyClient CreateRetryingClient(RecordingHttpMessageHandler handler)
  {
    var retry = new HevyRetryHandler((_, _) => Task.CompletedTask, () => 0d, TimeProvider.System)
    {
      InnerHandler = handler,
    };
    return new HevyClient(new HttpClient(retry), new HevyClientOptions("test-api-key"));
  }

  private sealed class DelayingHandler : HttpMessageHandler
  {
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
      throw new InvalidOperationException("Unreachable.");
    }
  }
}
