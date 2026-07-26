using System.Net;
using System.Text;
using Hevy.Client;
using Hevy.Client.Errors;
using Hevy.Client.Http;
using Hevy.Client.Models;
using TestSupport;
using Xunit;

namespace Hevy.Client.Tests;

public sealed class HevyClientExerciseHistoryStreamingTests
{
  [Theory]
  [InlineData(100, 150, 101, null)]
  [InlineData(1_000, 1_050, 1_000, "item_safety_cap")]
  public async Task HistoryWindowStreamsAtMostOneThousandEntriesWithOneRequest(
      int limit,
      int sourceCount,
      int expectedScanned,
      string? expectedReason)
  {
    var payload = HistoryPayload(sourceCount);
    var stream = new TrackingReadStream(Encoding.UTF8.GetBytes(payload));
    var handler = HistoryHandler(stream);
    var client = CreateClient(handler);

    var result = await client.GetExerciseHistoryWindowAsync(
        "template-1",
        new ExerciseHistoryWindowRequest(0, limit),
        default);

    Assert.Equal(limit, result.Items.Count);
    Assert.True(result.Truncated);
    Assert.Equal(expectedScanned, result.ScannedItemCount);
    Assert.Equal(expectedReason, result.TruncationReason);
    Assert.True(stream.BytesRead < stream.SourceLength);
    Assert.True(stream.IsDisposed);
    Assert.Single(handler.Requests);
  }

  [Fact]
  public async Task HistoryWindowContinuationUsesOneRequestAndAStableEligibleOffsetPerInvocation()
  {
    var payload = HistoryPayload(250);
    var handler = HistoryHandler(() => new TrackingReadStream(Encoding.UTF8.GetBytes(payload)));
    var client = CreateClient(handler);

    var first = await client.GetExerciseHistoryWindowAsync(
        "template-1",
        new ExerciseHistoryWindowRequest(0, 100),
        default);
    var second = await client.GetExerciseHistoryWindowAsync(
        "template-1",
        new ExerciseHistoryWindowRequest(100, 100),
        default);

    Assert.Equal("workout-0001", first.Items[0].WorkoutId);
    Assert.Equal("workout-0101", second.Items[0].WorkoutId);
    Assert.All([first, second], result =>
    {
      Assert.Equal(100, result.Items.Count);
      Assert.True(result.Truncated);
      Assert.Null(result.TruncationReason);
      Assert.InRange(result.ScannedItemCount, 1, ExerciseHistoryWindowRequest.MaximumScannedItems);
    });
    Assert.Equal(2, handler.Requests.Count);
  }

  [Fact]
  public async Task HistoryWindowScansPastIneligibleEntriesOnlyWithinTheOneThousandItemBudget()
  {
    var payload = HistoryPayload(160, index => index <= 50 ? "2026-06-30T23:00:00Z" : "2026-07-01T00:00:00Z");
    var handler = HistoryHandler(() => new TrackingReadStream(Encoding.UTF8.GetBytes(payload)));
    var client = CreateClient(handler);

    var result = await client.GetExerciseHistoryWindowAsync(
        "template-1",
        new ExerciseHistoryWindowRequest(
            0,
            100,
            EligibleStartTime: DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
            EligibleEndTime: DateTimeOffset.Parse("2026-07-02T00:00:00Z")),
        default);

    Assert.Equal(100, result.Items.Count);
    Assert.True(result.Truncated);
    Assert.Null(result.TruncationReason);
    Assert.Equal(151, result.ScannedItemCount);
  }

  [Fact]
  public async Task HistoryWindowStopsAtTheConfiguredByteCapWithoutPresentingAPrefixAsComplete()
  {
    var stream = new TrackingReadStream(Encoding.UTF8.GetBytes(HistoryPayload(20, new string('x', 400))));
    var handler = HistoryHandler(stream);
    var client = CreateClient(handler, new ExerciseHistoryReadLimits(1_024));

    var result = await client.GetExerciseHistoryWindowAsync(
        "template-1",
        new ExerciseHistoryWindowRequest(0, 10),
        default);

    Assert.True(result.Truncated);
    Assert.Equal("byte_safety_cap", result.TruncationReason);
    Assert.InRange(stream.BytesRead, 1, 1_024);
    Assert.Single(handler.Requests);
  }

  [Fact]
  public async Task HistoryWindowPropagatesCancellationWhileReadingTheResponseStream()
  {
    var prefix = Encoding.UTF8.GetBytes("{\"exercise_history\":[{\"workout_id\":\"blocked");
    var stream = new BlockingReadStream(prefix);
    var handler = HistoryHandler(stream);
    var client = CreateClient(handler);
    using var cancellation = new CancellationTokenSource();

    var read = client.GetExerciseHistoryWindowAsync(
        "template-1",
        new ExerciseHistoryWindowRequest(0, 10),
        cancellation.Token);
    await stream.Blocked;
    cancellation.Cancel();

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read);
    Assert.Single(handler.Requests);
  }

  [Fact]
  public async Task HistoryWindowRejectsAnOffsetBeyondTheScanBudgetBeforeHttpIo()
  {
    var handler = HistoryHandler(new TrackingReadStream(Encoding.UTF8.GetBytes(HistoryPayload(1))));
    var client = CreateClient(handler);

    await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.GetExerciseHistoryWindowAsync(
        "template-1",
        new ExerciseHistoryWindowRequest(1_000, 1),
        default));

    Assert.Empty(handler.Requests);
  }

  [Fact]
  public async Task HistoryWindowMapsMalformedStreamingJsonToTheSafeResponseError()
  {
    var handler = HistoryHandler(new TrackingReadStream(Encoding.UTF8.GetBytes("{\"exercise_history\":[{\"workout_id\":}]}")));
    var client = CreateClient(handler);

    var exception = await Assert.ThrowsAsync<HevyException>(() => client.GetExerciseHistoryWindowAsync(
        "template-1",
        new ExerciseHistoryWindowRequest(0, 10),
        default));

    Assert.Equal("unexpected_response", exception.Code);
    Assert.Single(handler.Requests);
  }

  private static HevyClient CreateClient(
      RecordingHttpMessageHandler handler,
      ExerciseHistoryReadLimits? limits = null) =>
      new(new HttpClient(handler), new HevyClientOptions("test-api-key"), limits ?? ExerciseHistoryReadLimits.Default);

  private static RecordingHttpMessageHandler HistoryHandler(Stream stream) => HistoryHandler(() => stream);

  private static RecordingHttpMessageHandler HistoryHandler(Func<Stream> streamFactory) => new((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
  {
    Content = new StreamContent(streamFactory()),
  });

  private static string HistoryPayload(int count, string? titleSuffix = null) =>
      HistoryPayload(count, _ => "2026-07-01T00:00:00Z", titleSuffix);

  private static string HistoryPayload(int count, Func<int, string> startTime, string? titleSuffix = null) =>
      $$"""
      {"exercise_history":[{{string.Join(',', Enumerable.Range(1, count).Select(index => Entry(index, startTime(index), titleSuffix)))}}]}
      """;

  private static string Entry(int index, string startTime, string? titleSuffix) =>
      $$"""{"workout_id":"workout-{{index:D4}}","workout_title":"Workout {{index}}{{titleSuffix}}","workout_start_time":"{{startTime}}","workout_end_time":"2026-07-01T01:00:00Z","exercise_template_id":"template-1","weight_kg":100,"reps":5,"distance_meters":null,"duration_seconds":null,"rpe":8,"custom_metric":null,"set_type":"normal"}""";

  private sealed class TrackingReadStream(byte[] bytes) : MemoryStream(bytes)
  {
    internal long SourceLength { get; } = bytes.Length;
    internal long BytesRead { get; private set; }
    internal bool IsDisposed { get; private set; }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
      var read = await base.ReadAsync(buffer, cancellationToken);
      BytesRead += read;
      return read;
    }

    protected override void Dispose(bool disposing)
    {
      IsDisposed = true;
      base.Dispose(disposing);
    }
  }

  private sealed class BlockingReadStream(byte[] prefix) : Stream
  {
    private int position;
    private readonly TaskCompletionSource blocked = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal Task Blocked => blocked.Task;
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => position; set => throw new NotSupportedException(); }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
      if (position < prefix.Length)
      {
        var count = Math.Min(buffer.Length, prefix.Length - position);
        prefix.AsMemory(position, count).CopyTo(buffer);
        position += count;
        return count;
      }

      blocked.TrySetResult();
      await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
      return 0;
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
  }
}
