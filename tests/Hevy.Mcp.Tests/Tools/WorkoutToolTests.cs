using Hevy.Client;
using Hevy.Client.Models;
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

    Assert.True(result.IsError);
    Assert.Equal("validation_error", result.Structured().GetProperty("error").GetProperty("code").GetString());
    Assert.Equal(0, client.CallCount);
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

    Assert.False(result.IsError);
    Assert.Equal("workout-1", item.GetProperty("id").GetString());
    Assert.False(item.TryGetProperty("exercises", out _));
    Assert.Equal(2, structured.GetProperty("meta").GetProperty("page").GetInt32());
    Assert.Equal(4, structured.GetProperty("meta").GetProperty("page_count").GetInt32());
    Assert.Equal(3, structured.GetProperty("meta").GetProperty("page_size").GetInt32());
    Assert.NotEmpty(result.Content);
  }

  [Fact]
  public async Task GetWorkoutsFullDetailIncludesNestedRecords()
  {
    var client = new FakeHevyClient
    {
      Workouts = new PagedResult<Workout>(1, 1, [FakeHevyClient.SampleWorkout()]),
    };

    var result = await WorkoutReadTools.GetWorkouts(Services(client), 1, 10, "full", CancellationToken.None);

    Assert.Equal("template-1", result.Structured().GetProperty("data").GetProperty("items")[0]
        .GetProperty("exercises")[0].GetProperty("exercise_template_id").GetString());
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

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    Assert.Equal(cancellation.Token, client.LastCancellationToken);
  }

  private static IServiceProvider Services(IHevyClient client) => new ServiceCollection()
      .AddSingleton(client)
      .BuildServiceProvider();
}
