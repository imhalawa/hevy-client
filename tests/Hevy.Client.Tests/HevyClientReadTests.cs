using System.Net;
using Hevy.Client;
using Hevy.Client.Http;
using Hevy.Client.Models;
using TestSupport;
using Xunit;

namespace Hevy.Client.Tests;

public sealed class HevyClientReadTests
{
  // Break caught: requests not going to Hevy with one authentication header, or a list response not being normalized.
  [Fact]
  public async Task Get_workouts_sends_the_official_authenticated_request_and_returns_a_page()
  {
    var handler = RespondingWith(Fixture.Read("workout-page.json"));
    var client = CreateClient(handler);

    var page = await client.GetWorkoutsAsync(2, 5, CancellationToken.None);

    var request = Assert.Single(handler.Requests);
    Assert.Equal(HttpMethod.Get, request.Method);
    Assert.Equal("https://api.hevyapp.com/v1/workouts?page=2&pageSize=5", request.RequestUri!.AbsoluteUri);
    Assert.Null(request.Body);
    Assert.True(request.Headers.TryGetValue("api-key", out var keys));
    Assert.Equal(["test-api-key"], keys);
    Assert.Equal(1, page.Page);
    Assert.Equal(2, page.PageCount);
    Assert.Equal("workout-page-1", page.Items[0].Id);
  }

  // Break caught: a supplied HttpClient base address being used to exfiltrate authenticated traffic.
  [Fact]
  public async Task Get_user_info_overrides_a_supplied_base_address_with_the_fixed_hevy_origin()
  {
    var handler = RespondingWith(Fixture.Read("user-info.json"));
    var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://untrusted.example/") };
    var client = new HevyClient(httpClient, new HevyClientOptions("test-api-key"));

    var user = await client.GetUserInfoAsync(CancellationToken.None);

    Assert.Equal("user-1", user.Id);
    Assert.Equal("https://api.hevyapp.com/v1/user/info", Assert.Single(handler.Requests).RequestUri!.AbsoluteUri);
  }

  // Break caught: changing the injected client's base address after construction sends the API key to another origin.
  [Fact]
  public async Task Get_user_info_rejects_a_post_construction_base_address_change_before_network_access()
  {
    var handler = RespondingWith(Fixture.Read("user-info.json"));
    var httpClient = new HttpClient(handler);
    var client = new HevyClient(httpClient, new HevyClientOptions("test-api-key"));
    httpClient.BaseAddress = new Uri("https://untrusted.example/");

    var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetUserInfoAsync(CancellationToken.None));

    Assert.Empty(handler.Requests);
    Assert.DoesNotContain("test-api-key", exception.ToString(), StringComparison.Ordinal);
  }

  // Break caught: invalid pagination reaching the remote API instead of being rejected locally.
  [Theory]
  [InlineData(0, 5)]
  [InlineData(1, 0)]
  [InlineData(1, 11)]
  public async Task Get_workouts_rejects_invalid_pagination_before_sending_a_request(int page, int pageSize)
  {
    var handler = RespondingWith(Fixture.Read("workout-page.json"));
    var client = CreateClient(handler);

    await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.GetWorkoutsAsync(page, pageSize, CancellationToken.None));

    Assert.Empty(handler.Requests);
  }

  // Break caught: routes that omit query values, fail to encode identifiers, or use an undocumented endpoint path.
  [Fact]
  public async Task Get_read_endpoints_use_their_documented_paths_and_query_values()
  {
    var responses = new Queue<string>([
        Fixture.Read("workout-count.json"),
            Fixture.Read("workout-events.json"),
            Fixture.Read("workout.json"),
            Fixture.Read("routine-page.json"),
            Fixture.Read("routine-response.json"),
            Fixture.Read("exercise-template-page.json"),
            Fixture.Read("exercise-template.json"),
            Fixture.Read("routine-folder-page.json"),
            Fixture.Read("routine-folder.json"),
            Fixture.Read("exercise-history-response.json"),
            Fixture.Read("body-measurement-page.json"),
            Fixture.Read("body-measurement.json")]);
    var handler = new RecordingHttpMessageHandler((_, _) => RecordingHttpMessageHandler.Json(HttpStatusCode.OK, responses.Dequeue()));
    var client = CreateClient(handler);

    Assert.Equal(42, await client.GetWorkoutCountAsync(CancellationToken.None));
    await client.GetWorkoutEventsAsync(3, 4, new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero), CancellationToken.None);
    Assert.Equal("workout-1", (await client.GetWorkoutAsync("a/b", CancellationToken.None)).Id);
    await client.GetRoutinesAsync(4, 5, CancellationToken.None);
    Assert.Equal("routine-response-1", (await client.GetRoutineAsync("routine/1", CancellationToken.None)).Id);
    await client.GetExerciseTemplatesAsync(5, 50, CancellationToken.None);
    Assert.Equal("D04AC939", (await client.GetExerciseTemplateAsync("template/1", CancellationToken.None)).Id);
    await client.GetRoutineFoldersAsync(6, 7, CancellationToken.None);
    Assert.Equal(42, (await client.GetRoutineFolderAsync(42, CancellationToken.None)).Id);
    await client.GetExerciseHistoryAsync("template/1", 1, 5, new DateOnly(2024, 1, 2), new DateOnly(2024, 2, 3), CancellationToken.None);
    await client.GetBodyMeasurementsAsync(7, 8, CancellationToken.None);
    Assert.Equal(new DateOnly(2024, 8, 14), (await client.GetBodyMeasurementAsync(new DateOnly(2024, 8, 14), CancellationToken.None)).Date);

    Assert.Equal(
    [
        "https://api.hevyapp.com/v1/workouts/count",
            "https://api.hevyapp.com/v1/workouts/events?page=3&pageSize=4&since=2024-01-02T03%3A04%3A05.0000000%2B00%3A00",
            "https://api.hevyapp.com/v1/workouts/a%2Fb",
            "https://api.hevyapp.com/v1/routines?page=4&pageSize=5",
            "https://api.hevyapp.com/v1/routines/routine%2F1",
            "https://api.hevyapp.com/v1/exercise_templates?page=5&pageSize=50",
            "https://api.hevyapp.com/v1/exercise_templates/template%2F1",
            "https://api.hevyapp.com/v1/routine_folders?page=6&pageSize=7",
            "https://api.hevyapp.com/v1/routine_folders/42",
            "https://api.hevyapp.com/v1/exercise_history/template%2F1?start_date=2024-01-02&end_date=2024-02-03",
            "https://api.hevyapp.com/v1/body_measurements?page=7&pageSize=8",
            "https://api.hevyapp.com/v1/body_measurements/2024-08-14",
        ],
    handler.Requests.Select(request => request.RequestUri!.AbsoluteUri));
  }

  // Break caught: exercise-history pagination reporting impossible metadata while returning the entire unpaginated payload.
  [Fact]
  public async Task Get_exercise_history_applies_local_pagination_without_undocumented_query_parameters()
  {
    var handler = RespondingWith(Fixture.Read("exercise-history-three.json"));
    var client = CreateClient(handler);

    var page = await client.GetExerciseHistoryAsync("D04AC939", 2, 2, null, null, CancellationToken.None);

    Assert.Equal("workout-history-3", Assert.Single(page.Items).WorkoutId);
    Assert.False(page.Truncated);
    Assert.Equal(
        "https://api.hevyapp.com/v1/exercise_history/D04AC939",
        Assert.Single(handler.Requests).RequestUri!.AbsoluteUri);
  }

  [Fact]
  public async Task Get_exercise_history_window_returns_a_bounded_response_with_one_http_request()
  {
    var handler = RespondingWith(Fixture.Read("exercise-history-three.json"));
    var client = CreateClient(handler);

    var history = await client.GetExerciseHistoryWindowAsync(
        "D04AC939",
        new ExerciseHistoryWindowRequest(0, 3, new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31)),
        CancellationToken.None);

    Assert.Equal(3, history.Items.Count);
    Assert.False(history.Truncated);
    Assert.Equal(["workout-history-1", "workout-history-2", "workout-history-3"], history.Items.Select(static entry => entry.WorkoutId));
    Assert.Equal(
        "https://api.hevyapp.com/v1/exercise_history/D04AC939?start_date=2024-01-01&end_date=2024-12-31",
        Assert.Single(handler.Requests).RequestUri!.AbsoluteUri);
  }

  // Break caught: options or public diagnostics exposing the API credential.
  [Fact]
  public void Authentication_configuration_never_formats_the_api_key()
  {
    var options = new HevyClientOptions("test-api-key");

    Assert.DoesNotContain("test-api-key", options.ToString(), StringComparison.Ordinal);
    Assert.Throws<ArgumentException>(() => new HevyClientOptions(" "));
  }

  // Break caught: a pre-existing header surviving authentication middleware and sending multiple credentials.
  [Fact]
  public async Task Authentication_handler_replaces_any_existing_api_key_with_exactly_one_configured_value()
  {
    var recordingHandler = RespondingWith(Fixture.Read("user-info.json"));
    var authenticationHandler = new HevyAuthenticationHandler(new HevyClientOptions("test-api-key"))
    {
      InnerHandler = recordingHandler,
    };
    using var httpClient = new HttpClient(authenticationHandler) { BaseAddress = new Uri("https://api.hevyapp.com/") };
    using var request = new HttpRequestMessage(HttpMethod.Get, "v1/user/info");
    request.Headers.TryAddWithoutValidation("api-key", "incorrect-key");

    await httpClient.SendAsync(request, CancellationToken.None);

    var headers = Assert.Single(recordingHandler.Requests).Headers;
    Assert.True(headers.TryGetValue("api-key", out var keys));
    Assert.Equal(["test-api-key"], keys);
  }

  // Break caught: standalone authentication middleware attaching the credential to a non-Hevy absolute request.
  [Theory]
  [InlineData("https://untrusted.example/v1/user/info")]
  [InlineData("http://api.hevyapp.com/v1/user/info")]
  [InlineData("https://api.hevyapp.com:444/v1/user/info")]
  public async Task Authentication_handler_rejects_an_unsafe_target_before_adding_the_api_key(string target)
  {
    var recordingHandler = RespondingWith(Fixture.Read("user-info.json"));
    var authenticationHandler = new HevyAuthenticationHandler(new HevyClientOptions("test-api-key"))
    {
      InnerHandler = recordingHandler,
    };
    using var httpClient = new HttpClient(authenticationHandler);

    var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
        httpClient.GetAsync(target, CancellationToken.None));

    Assert.Empty(recordingHandler.Requests);
    Assert.DoesNotContain("test-api-key", exception.ToString(), StringComparison.Ordinal);
  }

  // Break caught: the production HTTP stack following a redirect that could replay a custom credential cross-origin.
  [Fact]
  public void Production_pipeline_disables_automatic_redirects()
  {
    using var pipeline = HevyClient.CreateProductionPipeline(new HevyClientOptions("test-api-key"));

    var authenticationHandler = Assert.IsType<HevyAuthenticationHandler>(pipeline.InnerHandler);
    var primaryHandler = Assert.IsType<HttpClientHandler>(authenticationHandler.InnerHandler);
    Assert.False(primaryHandler.AllowAutoRedirect);
  }

  private static HevyClient CreateClient(RecordingHttpMessageHandler handler) =>
      new(new HttpClient(handler), new HevyClientOptions("test-api-key"));

  private static RecordingHttpMessageHandler RespondingWith(string response) =>
      new((_, _) => RecordingHttpMessageHandler.Json(HttpStatusCode.OK, response));
}
