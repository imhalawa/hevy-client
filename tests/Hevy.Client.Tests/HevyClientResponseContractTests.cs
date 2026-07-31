using System.Net;
using System.Text.Json.Nodes;
using Hevy.Client.Errors;
using TestSupport;
using Xunit;

namespace Hevy.Client.Tests;

public sealed class HevyClientResponseContractTests
{
  public static TheoryData<string, string> NullCollectionMembers => new()
  {
    { "workouts", "{\"page\":1,\"page_count\":1,\"workouts\":[null]}" },
    { "events", "{\"page\":1,\"page_count\":1,\"events\":[null]}" },
    { "routines", "{\"page\":1,\"page_count\":1,\"routines\":[null]}" },
    { "templates", "{\"page\":1,\"page_count\":1,\"exercise_templates\":[null]}" },
    { "folders", "{\"page\":1,\"page_count\":1,\"routine_folders\":[null]}" },
    { "measurements", "{\"page\":1,\"page_count\":1,\"body_measurements\":[null]}" },
    { "history", "{\"exercise_history\":[null]}" },
    { "workout", Mutate("workout.json", root => root["exercises"] = new JsonArray((JsonNode?)null)) },
    { "workout", Mutate("workout.json", root => root["exercises"]![0]!["sets"] = new JsonArray((JsonNode?)null)) },
    { "routine", MutateRoutine(root => root["exercises"] = new JsonArray((JsonNode?)null)) },
    { "routine", MutateRoutine(root => root["exercises"]![0]!["sets"] = new JsonArray((JsonNode?)null)) },
    { "template", Mutate("exercise-template.json", root => root["secondary_muscle_groups"] = new JsonArray((JsonNode?)null)) },
  };

  public static TheoryData<string, string> MissingDownstreamMembers => new()
  {
    { "user", Mutate("user-info.json", root => root["data"]!["name"] = null) },
    { "count", "{}" },
    { "workout", Mutate("workout.json", root => root["title"] = null) },
    { "events", "{\"page\":1,\"page_count\":1,\"events\":[{\"id\":\"workout-1\",\"deleted_at\":\"2024-08-14T12:00:00Z\"}]}" },
    { "events", "{\"page\":1,\"page_count\":1,\"events\":[{\"type\":\"deleted\",\"id\":\"workout-1\"}]}" },
    { "routine", Mutate("routine-response.json", root => root["routine"]!["title"] = null) },
    { "template", Mutate("exercise-template.json", root => root.Remove("title")) },
    { "folder", Mutate("routine-folder.json", root => root["title"] = null) },
    { "history", Mutate("exercise-history-response.json", root => root["exercise_history"]![0]!["workout_title"] = null) },
    { "measurement", "{\"weight_kg\":80}" },
  };

  public static TheoryData<string, string> ImpossiblePages => new()
  {
    { "workouts", Mutate("workout-page.json", root => root["page_count"] = 0) },
    { "events", Mutate("workout-events.json", root => root["page_count"] = 0) },
    { "routines", Mutate("routine-page.json", root => { root["page"] = 1; root["page_count"] = 0; }) },
    { "templates", Mutate("exercise-template-page.json", root => root["page_count"] = 0) },
    { "folders", Mutate("routine-folder-page.json", root => { root["page"] = 1; root["page_count"] = 0; }) },
    { "measurements", Mutate("body-measurement-page.json", root => root["page_count"] = 0) },
  };

  public static TheoryData<string, string, int> CanonicalEmptyPages => new()
  {
    { "workouts", EmptyPage("workouts", 1, 0, includeAdditiveField: true), 1 },
    { "events", EmptyPage("events", 1, 0, includeAdditiveField: true), 1 },
    { "routines", EmptyPage("routines", 1, 0, includeAdditiveField: true), 1 },
    { "templates", EmptyPage("exercise_templates", 1, 0, includeAdditiveField: true), 1 },
    { "folders", EmptyPage("routine_folders", 1, 0, includeAdditiveField: true), 1 },
    { "measurements", EmptyPage("body_measurements", 1, 0, includeAdditiveField: true), 1 },
  };

  public static TheoryData<string, string, int> EmptyPositiveCountPages => new()
  {
    { "workouts", EmptyPage("workouts", 1, 1), 1 },
    { "workouts", EmptyPage("workouts", 2, 2), 2 },
    { "events", EmptyPage("events", 1, 1), 1 },
    { "events", EmptyPage("events", 2, 2), 2 },
    { "routines", EmptyPage("routines", 1, 1), 1 },
    { "routines", EmptyPage("routines", 2, 2), 2 },
    { "templates", EmptyPage("exercise_templates", 1, 1), 1 },
    { "templates", EmptyPage("exercise_templates", 2, 2), 2 },
    { "folders", EmptyPage("routine_folders", 1, 1), 1 },
    { "folders", EmptyPage("routine_folders", 2, 2), 2 },
    { "measurements", EmptyPage("body_measurements", 1, 1), 1 },
    { "measurements", EmptyPage("body_measurements", 2, 2), 2 },
  };

  public static TheoryData<string, string, int> NonemptyAdditivePages => new()
  {
    { "workouts", AddFuturePageFields("workout-page.json", "workouts"), 1 },
    { "events", AddFuturePageFields("workout-events.json", "events"), 1 },
    { "routines", AddFuturePageFields("routine-page.json", "routines"), 1 },
    { "templates", AddFuturePageFields("exercise-template-page.json", "exercise_templates"), 1 },
    { "folders", AddFuturePageFields("routine-folder-page.json", "routine_folders"), 2 },
    { "measurements", AddFuturePageFields("body-measurement-page.json", "body_measurements"), 1 },
  };

  [Theory]
  [MemberData(nameof(NullCollectionMembers))]
  public async Task Null_elements_in_every_response_collection_are_rejected_at_the_client_boundary(string operation, string response) =>
      await AssertUnexpectedResponseAsync(operation, response);

  [Theory]
  [MemberData(nameof(MissingDownstreamMembers))]
  public async Task Missing_or_null_nonnullable_response_members_are_rejected_before_tools_can_consume_them(string operation, string response) =>
      await AssertUnexpectedResponseAsync(operation, response);

  [Theory]
  [MemberData(nameof(ImpossiblePages))]
  public async Task Nonempty_zero_count_pages_are_rejected_for_every_paginated_response_family(string operation, string response) =>
      await AssertUnexpectedResponseAsync(operation, response);

  [Theory]
  [MemberData(nameof(CanonicalEmptyPages))]
  public async Task Empty_collection_is_valid_only_for_the_canonical_first_page_of_every_paginated_response_family(
      string operation,
      string response,
      int requestedPage)
  {
    var client = CreateClient(RespondingWith(response));

    await InvokeAsync(client, operation, requestedPage);
  }

  [Theory]
  [MemberData(nameof(EmptyPositiveCountPages))]
  public async Task Empty_collection_with_positive_page_count_is_rejected_on_first_and_later_pages_for_every_family(
      string operation,
      string response,
      int requestedPage)
  {
    var client = CreateClient(RespondingWith(response));

    var exception = (await FluentActions.Awaiting(() => InvokeAsync(client, operation, requestedPage)).Should().ThrowExactlyAsync<HevyException>()).Which;

    (exception.Code).Should().Be("unexpected_response");
    (exception.IsRetryable).Should().BeFalse();
    (exception.StatusCode).Should().Be(HttpStatusCode.OK);
  }

  [Theory]
  [MemberData(nameof(NonemptyAdditivePages))]
  public async Task Nonempty_pages_with_additive_fields_remain_valid_for_every_paginated_response_family(
      string operation,
      string response,
      int requestedPage)
  {
    var client = CreateClient(RespondingWith(response));

    await InvokeAsync(client, operation, requestedPage);
  }

  private static async Task AssertUnexpectedResponseAsync(string operation, string response)
  {
    var client = CreateClient(RespondingWith(response));

    var exception = (await FluentActions.Awaiting(() => InvokeAsync(client, operation)).Should().ThrowExactlyAsync<HevyException>()).Which;

    (exception.Code).Should().Be("unexpected_response");
    (exception.IsRetryable).Should().BeFalse();
    (exception.StatusCode).Should().Be(HttpStatusCode.OK);
  }

  private static Task InvokeAsync(HevyClient client, string operation, int page = 1) => operation switch
  {
    "user" => client.GetUserInfoAsync(default),
    "count" => client.GetWorkoutCountAsync(default),
    "workouts" => client.GetWorkoutsAsync(page, 10, default),
    "events" => client.GetWorkoutEventsAsync(page, 10, DateTimeOffset.UnixEpoch, default),
    "workout" => client.GetWorkoutAsync("workout-1", default),
    "routines" => client.GetRoutinesAsync(page, 10, default),
    "routine" => client.GetRoutineAsync("routine-1", default),
    "templates" => client.GetExerciseTemplatesAsync(page, 100, default),
    "template" => client.GetExerciseTemplateAsync("template-1", default),
    "folders" => client.GetRoutineFoldersAsync(page, 10, default),
    "folder" => client.GetRoutineFolderAsync(1, default),
    "history" => client.GetExerciseHistoryAsync("template-1", 1, 10, null, null, default),
    "measurements" => client.GetBodyMeasurementsAsync(page, 10, default),
    "measurement" => client.GetBodyMeasurementAsync(new DateOnly(2024, 8, 14), default),
    _ => throw new ArgumentOutOfRangeException(nameof(operation)),
  };

  private static string Mutate(string fixture, Action<JsonObject> mutation)
  {
    var root = JsonNode.Parse(Fixture.Read(fixture))!.AsObject();
    mutation(root);
    return root.ToJsonString();
  }

  private static string MutateRoutine(Action<JsonObject> mutation)
  {
    var routine = JsonNode.Parse(Fixture.Read("routine.json"))!.AsObject();
    mutation(routine);
    return new JsonObject { ["routine"] = routine }.ToJsonString();
  }

  private static string EmptyPage(string collectionName, int page, int pageCount, bool includeAdditiveField = false)
  {
    var response = new JsonObject
    {
      ["page"] = page,
      ["page_count"] = pageCount,
      [collectionName] = new JsonArray(),
    };
    if (includeAdditiveField)
    {
      response["future_page_field"] = "ignored";
    }

    return response.ToJsonString();
  }

  private static string AddFuturePageFields(string fixture, string collectionName) => Mutate(fixture, root =>
  {
    root["future_page_field"] = "ignored";
    var collection = root[collectionName]!.AsArray();
    collection[0]!["future_item_field"] = "ignored";
  });

  private static HevyClient CreateClient(RecordingHttpMessageHandler handler) =>
      new(new HttpClient(handler), new HevyClientOptions("test-api-key"));

  private static RecordingHttpMessageHandler RespondingWith(string response) =>
      new((_, _) => RecordingHttpMessageHandler.Json(HttpStatusCode.OK, response));
}
