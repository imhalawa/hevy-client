using Hevy.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using TestSupport;
using Xunit;

namespace Hevy.Mcp.Tests.Tools;

public sealed class WorkoutToolTests
{
  [Theory]
  [InlineData(0, 10)]
  [InlineData(1, 0)]
  [InlineData(1, 11)]
  public async Task GetWorkoutsRejectsInvalidPaginationBeforeClientIo(int page, int pageSize)
  {
    var client = new FakeHevyClient();

    var result = await WorkoutReadTools.GetWorkouts(Services(client), page, pageSize, "compact", CancellationToken.None);

    (result.IsError).Should().BeTrue();
    (result.Structured().GetProperty("error").GetProperty("code").GetString()).Should().Be("validation_error");
    (client.CallCount).Should().Be(0);
  }

  [Fact]
  public async Task GetWorkoutsReturnsCompactItemsAndPaginationByDefault()
  {
    var client = new FakeHevyClient
    {
      Workouts = new PagedResult<Workout>(2, 4, [FakeHevyClient.SampleWorkout()]),
    };

    var result = await WorkoutReadTools.GetWorkouts(Services(client), 2, 3, "compact", CancellationToken.None);
    var structured = result.Structured();
    var item = structured.GetProperty("data").GetProperty("items")[0];

    (result.IsError).Should().BeFalse();
    (item.GetProperty("id").GetString()).Should().Be("workout-1");
    (item.TryGetProperty("exercises", out _)).Should().BeFalse();
    (structured.GetProperty("meta").GetProperty("page").GetInt32()).Should().Be(2);
    (structured.GetProperty("meta").GetProperty("page_count").GetInt32()).Should().Be(4);
    (structured.GetProperty("meta").GetProperty("page_size").GetInt32()).Should().Be(3);
    (structured.GetProperty("meta").GetProperty("truncated").GetBoolean()).Should().BeTrue();
    (structured.GetProperty("meta").GetProperty("continuation").GetProperty("page").GetInt32()).Should().Be(3);
    (structured.GetProperty("meta").GetProperty("continuation").GetProperty("page_size").GetInt32()).Should().Be(3);
    (result.Content).Should().NotBeEmpty();
  }

  [Fact]
  public async Task GetWorkoutsFullDetailIncludesNestedRecords()
  {
    var client = new FakeHevyClient
    {
      Workouts = new PagedResult<Workout>(1, 1, [FakeHevyClient.SampleWorkout()]),
    };

    var result = await WorkoutReadTools.GetWorkouts(Services(client), 1, 10, "full", CancellationToken.None);

    (result.Structured().GetProperty("data").GetProperty("items")[0]
        .GetProperty("exercises")[0].GetProperty("exercise_template_id").GetString()).Should().Be("template-1");
  }

  [Fact]
  public async Task GetWorkoutsPropagatesMcpCancellationToTheClient()
  {
    using var cancellation = new CancellationTokenSource();
    var client = new FakeHevyClient
    {
      GetWorkoutsHandler = async (_, _, token) =>
      {
        await Task.Delay(Timeout.InfiniteTimeSpan, token);
        return new PagedResult<Workout>(1, 0, []);
      },
    };

    var pending = WorkoutReadTools.GetWorkouts(Services(client), 1, 10, "compact", cancellation.Token);
    await cancellation.CancelAsync();

    await FluentActions.Awaiting(() => pending).Should().ThrowAsync<OperationCanceledException>();
    (client.LastCancellationToken).Should().Be(cancellation.Token);
  }

  private static IServiceProvider Services(IHevyClient client) => new ServiceCollection()
      .AddSingleton(client)
      .BuildServiceProvider();
}
