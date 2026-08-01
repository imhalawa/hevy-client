using System.Net;
using Hevy.Core.Exceptions;
using Hevy.Client.Http;
using TestSupport;
using Xunit;

namespace Hevy.Client.Tests;

public sealed class HevyClientReadTests
{
  [Fact]
  public async Task Get_workouts_sends_the_official_authenticated_request_and_returns_a_page()
  {
    var handler = RespondingWith(Fixture.Read("workout-page.json"));
    var client = CreateClient(handler);

    var page = await client.GetWorkoutsAsync(1, 5, CancellationToken.None);

    var request = (handler.Requests).Should().ContainSingle().Which;
    (request.Method).Should().Be(HttpMethod.Get);
    (request.RequestUri!.AbsoluteUri).Should().Be("https://api.hevyapp.com/v1/workouts?page=1&pageSize=5");
    (request.Body).Should().BeNull();
    (request.Headers.TryGetValue("api-key", out var keys)).Should().BeTrue();
    (keys).Should().Equal(["test-api-key"]);
    (page.Page).Should().Be(1);
    (page.PageCount).Should().Be(2);
    (page.Items[0].Id).Should().Be("workout-page-1");
  }

  [Theory]
  [InlineData("{\"page\":2,\"page_count\":2,\"workouts\":[]}")]
  public async Task Get_workouts_rejects_inconsistent_pages(string response)
  {
    var handler = RespondingWith(response);
    var client = CreateClient(handler);

    var exception = (await FluentActions.Awaiting(() => client.GetWorkoutsAsync(1, 1, CancellationToken.None)).Should().ThrowExactlyAsync<HevyException>()).Which;

    (exception.Code).Should().Be("unexpected_response");
  }

  [Fact]
  public async Task Get_workouts_rejects_more_items_than_the_requested_page_size()
  {
    using var fixture = System.Text.Json.JsonDocument.Parse(Fixture.Read("workout-page.json"));
    var item = fixture.RootElement.GetProperty("workouts")[0].GetRawText();
    var handler = RespondingWith($"{{\"page\":1,\"page_count\":1,\"workouts\":[{item},{item}]}}");
    var client = CreateClient(handler);

    var exception = (await FluentActions.Awaiting(() => client.GetWorkoutsAsync(1, 1, CancellationToken.None)).Should().ThrowExactlyAsync<HevyException>()).Which;

    (exception.Code).Should().Be("unexpected_response");
  }

  [Fact]
  public async Task Get_workouts_rejects_a_later_page_when_page_count_is_zero()
  {
    var handler = RespondingWith("{\"page\":2,\"page_count\":0,\"workouts\":[]}");
    var client = CreateClient(handler);

    var exception = (await FluentActions.Awaiting(() => client.GetWorkoutsAsync(2, 1, CancellationToken.None)).Should().ThrowExactlyAsync<HevyException>()).Which;

    (exception.Code).Should().Be("unexpected_response");
  }

  [Fact]
  public async Task Get_user_info_overrides_a_supplied_base_address_with_the_fixed_hevy_origin()
  {
    var handler = RespondingWith(Fixture.Read("user-info.json"));
    var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://untrusted.example/") };
    var client = new HevyClient(httpClient, new HevyClientOptions("test-api-key"));

    var user = await client.GetUserInfoAsync(CancellationToken.None);

    (user.Id).Should().Be("user-1");
    ((handler.Requests).Should().ContainSingle().Which.RequestUri!.AbsoluteUri).Should().Be("https://api.hevyapp.com/v1/user/info");
  }

  [Fact]
  public async Task Get_user_info_rejects_a_post_construction_base_address_change_before_network_access()
  {
    var handler = RespondingWith(Fixture.Read("user-info.json"));
    var httpClient = new HttpClient(handler);
    var client = new HevyClient(httpClient, new HevyClientOptions("test-api-key"));
    httpClient.BaseAddress = new Uri("https://untrusted.example/");

    var exception = (await FluentActions.Awaiting(() => client.GetUserInfoAsync(CancellationToken.None)).Should().ThrowExactlyAsync<InvalidOperationException>()).Which;

    (handler.Requests).Should().BeEmpty();
    (exception.ToString()).Should().NotContain("test-api-key");
  }

  [Theory]
  [InlineData(0, 5)]
  [InlineData(1, 0)]
  [InlineData(1, 11)]
  public async Task Get_workouts_rejects_invalid_pagination_before_sending_a_request(int page, int pageSize)
  {
    var handler = RespondingWith(Fixture.Read("workout-page.json"));
    var client = CreateClient(handler);

    await FluentActions.Awaiting(() => client.GetWorkoutsAsync(page, pageSize, CancellationToken.None)).Should().ThrowExactlyAsync<ArgumentOutOfRangeException>();

    (handler.Requests).Should().BeEmpty();
  }

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

    (await client.GetWorkoutCountAsync(CancellationToken.None)).Should().Be(42);
    await client.GetWorkoutEventsAsync(1, 4, new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero), CancellationToken.None);
    ((await client.GetWorkoutAsync("a/b", CancellationToken.None)).Id).Should().Be("workout-1");
    await client.GetRoutinesAsync(1, 5, CancellationToken.None);
    ((await client.GetRoutineAsync("routine/1", CancellationToken.None)).Id).Should().Be("routine-response-1");
    await client.GetExerciseTemplatesAsync(1, 50, CancellationToken.None);
    ((await client.GetExerciseTemplateAsync("template/1", CancellationToken.None)).Id).Should().Be("D04AC939");
    await client.GetRoutineFoldersAsync(2, 7, CancellationToken.None);
    ((await client.GetRoutineFolderAsync(42, CancellationToken.None)).Id).Should().Be(42);
    await client.GetExerciseHistoryAsync("template/1", 1, 5, new DateOnly(2024, 1, 2), new DateOnly(2024, 2, 3), CancellationToken.None);
    await client.GetBodyMeasurementsAsync(1, 8, CancellationToken.None);
    ((await client.GetBodyMeasurementAsync(new DateOnly(2024, 8, 14), CancellationToken.None)).Date).Should().Be(new DateOnly(2024, 8, 14));

    (handler.Requests.Select(request => request.RequestUri!.AbsoluteUri)).Should().Equal([
        "https://api.hevyapp.com/v1/workouts/count",
            "https://api.hevyapp.com/v1/workouts/events?page=1&pageSize=4&since=2024-01-02T03%3A04%3A05.0000000%2B00%3A00",
            "https://api.hevyapp.com/v1/workouts/a%2Fb",
            "https://api.hevyapp.com/v1/routines?page=1&pageSize=5",
            "https://api.hevyapp.com/v1/routines/routine%2F1",
            "https://api.hevyapp.com/v1/exercise_templates?page=1&pageSize=50",
            "https://api.hevyapp.com/v1/exercise_templates/template%2F1",
            "https://api.hevyapp.com/v1/routine_folders?page=2&pageSize=7",
            "https://api.hevyapp.com/v1/routine_folders/42",
            "https://api.hevyapp.com/v1/exercise_history/template%2F1?start_date=2024-01-02&end_date=2024-02-03",
            "https://api.hevyapp.com/v1/body_measurements?page=1&pageSize=8",
            "https://api.hevyapp.com/v1/body_measurements/2024-08-14",
        ]);
  }

  [Fact]
  public async Task Get_exercise_history_applies_local_pagination_without_undocumented_query_parameters()
  {
    var handler = RespondingWith(Fixture.Read("exercise-history-three.json"));
    var client = CreateClient(handler);

    var page = await client.GetExerciseHistoryAsync("D04AC939", 2, 2, null, null, CancellationToken.None);

    ((page.Items).Should().ContainSingle().Which.WorkoutId).Should().Be("workout-history-3");
    (page.Truncated).Should().BeFalse();
    ((handler.Requests).Should().ContainSingle().Which.RequestUri!.AbsoluteUri).Should().Be("https://api.hevyapp.com/v1/exercise_history/D04AC939");
  }

  [Fact]
  public async Task Get_exercise_history_window_returns_a_bounded_response_with_one_http_request()
  {
    var handler = RespondingWith(Fixture.Read("exercise-history-three.json"));
    var client = CreateClient(handler);

    var history = await client.GetExerciseHistoryWindowAsync(
        "D04AC939",
        new ExerciseHistoryQuery(0, 3, new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31)),
        CancellationToken.None);

    (history.Items.Count).Should().Be(3);
    (history.Truncated).Should().BeFalse();
    (history.Items.Select(static entry => entry.WorkoutId)).Should().Equal(["workout-history-1", "workout-history-2", "workout-history-3"]);
    ((handler.Requests).Should().ContainSingle().Which.RequestUri!.AbsoluteUri).Should().Be("https://api.hevyapp.com/v1/exercise_history/D04AC939?start_date=2024-01-01&end_date=2024-12-31");
  }

  [Fact]
  public void Authentication_configuration_never_formats_the_api_key()
  {
    var options = new HevyClientOptions("test-api-key");

    (options.ToString()).Should().NotContain("test-api-key");
    FluentActions.Invoking(() => new HevyClientOptions(" ")).Should().ThrowExactly<ArgumentException>();
  }

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

    var headers = (recordingHandler.Requests).Should().ContainSingle().Which.Headers;
    (headers.TryGetValue("api-key", out var keys)).Should().BeTrue();
    (keys).Should().Equal(["test-api-key"]);
  }

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

    var exception = (await FluentActions.Awaiting(() =>
        httpClient.GetAsync(target, CancellationToken.None)).Should().ThrowExactlyAsync<InvalidOperationException>()).Which;

    (recordingHandler.Requests).Should().BeEmpty();
    (exception.ToString()).Should().NotContain("test-api-key");
  }

  [Fact]
  public void Production_pipeline_disables_automatic_redirects()
  {
    using var pipeline = HevyClient.CreateProductionPipeline(new HevyClientOptions("test-api-key"));

    var authenticationHandler = (pipeline.InnerHandler).Should().BeOfType<HevyAuthenticationHandler>().Which;
    var primaryHandler = (authenticationHandler.InnerHandler).Should().BeOfType<HttpClientHandler>().Which;
    (primaryHandler.AllowAutoRedirect).Should().BeFalse();
  }

  private static HevyClient CreateClient(RecordingHttpMessageHandler handler) =>
      new(new HttpClient(handler), new HevyClientOptions("test-api-key"));

  private static RecordingHttpMessageHandler RespondingWith(string response) =>
      new((_, _) => RecordingHttpMessageHandler.Json(HttpStatusCode.OK, response));
}
