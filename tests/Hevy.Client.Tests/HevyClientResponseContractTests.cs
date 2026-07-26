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

  [Fact]
  public async Task Canonical_empty_first_page_remains_valid()
  {
    var handler = RespondingWith("{\"page\":1,\"page_count\":0,\"workouts\":[]}");
    var client = CreateClient(handler);

    var result = await client.GetWorkoutsAsync(1, 10, CancellationToken.None);

    Assert.Empty(result.Items);
    Assert.Equal(0, result.PageCount);
  }

  private static async Task AssertUnexpectedResponseAsync(string operation, string response)
  {
    var client = CreateClient(RespondingWith(response));

    var exception = await Assert.ThrowsAsync<HevyException>(() => InvokeAsync(client, operation));

    Assert.Equal("unexpected_response", exception.Code);
    Assert.False(exception.IsRetryable);
    Assert.Equal(HttpStatusCode.OK, exception.StatusCode);
  }

  private static Task InvokeAsync(HevyClient client, string operation) => operation switch
  {
    "user" => client.GetUserInfoAsync(default),
    "count" => client.GetWorkoutCountAsync(default),
    "workouts" => client.GetWorkoutsAsync(1, 10, default),
    "events" => client.GetWorkoutEventsAsync(1, 10, DateTimeOffset.UnixEpoch, default),
    "workout" => client.GetWorkoutAsync("workout-1", default),
    "routines" => client.GetRoutinesAsync(1, 10, default),
    "routine" => client.GetRoutineAsync("routine-1", default),
    "templates" => client.GetExerciseTemplatesAsync(1, 100, default),
    "template" => client.GetExerciseTemplateAsync("template-1", default),
    "folders" => client.GetRoutineFoldersAsync(1, 10, default),
    "folder" => client.GetRoutineFolderAsync(1, default),
    "history" => client.GetExerciseHistoryAsync("template-1", 1, 10, null, null, default),
    "measurements" => client.GetBodyMeasurementsAsync(1, 10, default),
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

  private static HevyClient CreateClient(RecordingHttpMessageHandler handler) =>
      new(new HttpClient(handler), new HevyClientOptions("test-api-key"));

  private static RecordingHttpMessageHandler RespondingWith(string response) =>
      new((_, _) => RecordingHttpMessageHandler.Json(HttpStatusCode.OK, response));
}
