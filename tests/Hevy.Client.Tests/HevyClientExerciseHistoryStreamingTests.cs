using System.Net;
using System.Text;
using Hevy.Core.Exceptions;
using Hevy.Client.Http;
using TestSupport;
using Xunit;

namespace Hevy.Client.Tests;

public sealed class HevyClientExerciseHistoryStreamingTests
{
  [Fact]
  public async Task Fractional_history_reps_are_rejected_as_an_unexpected_response()
  {
    const string response = "{\"exercise_history\":[{\"workout_id\":\"workout-1\",\"workout_title\":\"Leg Day\",\"workout_start_time\":\"2024-08-14T12:00:00Z\",\"workout_end_time\":\"2024-08-14T13:00:00Z\",\"exercise_template_id\":\"template-1\",\"weight_kg\":100,\"reps\":5.5,\"distance_meters\":null,\"duration_seconds\":null,\"rpe\":8,\"custom_metric\":null,\"set_type\":\"normal\"}]}";
    var handler = new RecordingHttpMessageHandler((_, _) => RecordingHttpMessageHandler.Json(HttpStatusCode.OK, response));
    var client = new HevyClient(new HttpClient(handler), new HevyClientOptions("test-api-key"));

    var exception = (await FluentActions.Awaiting(() =>
        client.GetExerciseHistoryAsync("template-1", 1, 10, null, null, CancellationToken.None)).Should().ThrowExactlyAsync<HevyException>()).Which;

    (exception.Code).Should().Be("unexpected_response");
  }
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
        new ExerciseHistoryQuery(0, limit),
        default);

    (result.Items.Count).Should().Be(limit);
    (result.Truncated).Should().BeTrue();
    (result.ScannedItemCount).Should().Be(expectedScanned);
    (result.TruncationReason).Should().Be(expectedReason);
    (stream.BytesRead < stream.SourceLength).Should().BeTrue();
    (stream.IsDisposed).Should().BeTrue();
    (handler.Requests).Should().ContainSingle();
  }

  [Fact]
  public async Task HistoryWindowContinuationUsesOneRequestAndAStableEligibleOffsetPerInvocation()
  {
    var payload = HistoryPayload(250);
    var handler = HistoryHandler(() => new TrackingReadStream(Encoding.UTF8.GetBytes(payload)));
    var client = CreateClient(handler);

    var first = await client.GetExerciseHistoryWindowAsync(
        "template-1",
        new ExerciseHistoryQuery(0, 100),
        default);
    var second = await client.GetExerciseHistoryWindowAsync(
        "template-1",
        new ExerciseHistoryQuery(100, 100),
        default);

    (first.Items[0].WorkoutId).Should().Be("workout-0001");
    (second.Items[0].WorkoutId).Should().Be("workout-0101");
    (new[] { first, second }).Should().AllSatisfy(result =>
    {
      (result.Items.Count).Should().Be(100);
      (result.Truncated).Should().BeTrue();
      (result.TruncationReason).Should().BeNull();
      (result.ScannedItemCount).Should().BeInRange(1, ExerciseHistoryQuery.MaximumScannedItems);
    });
    (handler.Requests.Count).Should().Be(2);
  }

  [Fact]
  public async Task HistoryWindowScansPastIneligibleEntriesOnlyWithinTheOneThousandItemBudget()
  {
    var payload = HistoryPayload(160, index => index <= 50 ? "2026-06-30T23:00:00Z" : "2026-07-01T00:00:00Z");
    var handler = HistoryHandler(() => new TrackingReadStream(Encoding.UTF8.GetBytes(payload)));
    var client = CreateClient(handler);

    var result = await client.GetExerciseHistoryWindowAsync(
        "template-1",
        new ExerciseHistoryQuery(
            0,
            100,
            EligibleStartTime: DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
            EligibleEndTime: DateTimeOffset.Parse("2026-07-02T00:00:00Z")),
        default);

    (result.Items.Count).Should().Be(100);
    (result.Truncated).Should().BeTrue();
    (result.TruncationReason).Should().BeNull();
    (result.ScannedItemCount).Should().Be(151);
  }

  [Fact]
  public async Task HistoryWindowStopsAtTheConfiguredByteCapWithoutPresentingAPrefixAsComplete()
  {
    var stream = new TrackingReadStream(Encoding.UTF8.GetBytes(HistoryPayload(20, new string('x', 400))));
    var handler = HistoryHandler(stream);
    var client = CreateClient(handler, new ExerciseHistoryReadLimits(1_024));

    var result = await client.GetExerciseHistoryWindowAsync(
        "template-1",
        new ExerciseHistoryQuery(0, 10),
        default);

    (result.Truncated).Should().BeTrue();
    (result.TruncationReason).Should().Be("byte_safety_cap");
    (stream.BytesRead).Should().BeInRange(1, 1_024);
    (handler.Requests).Should().ContainSingle();
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
        new ExerciseHistoryQuery(0, 10),
        cancellation.Token);
    await stream.Blocked;
    cancellation.Cancel();

    await FluentActions.Awaiting(() => read).Should().ThrowAsync<OperationCanceledException>();
    (handler.Requests).Should().ContainSingle();
  }

  [Fact]
  public async Task HistoryWindowRejectsAnOffsetBeyondTheScanBudgetBeforeHttpIo()
  {
    var handler = HistoryHandler(new TrackingReadStream(Encoding.UTF8.GetBytes(HistoryPayload(1))));
    var client = CreateClient(handler);

    await FluentActions.Awaiting(() => client.GetExerciseHistoryWindowAsync(
        "template-1",
        new ExerciseHistoryQuery(1_000, 1),
        default)).Should().ThrowExactlyAsync<ArgumentOutOfRangeException>();

    (handler.Requests).Should().BeEmpty();
  }

  [Fact]
  public async Task HistoryWindowMapsMalformedStreamingJsonToTheSafeResponseError()
  {
    var handler = HistoryHandler(new TrackingReadStream(Encoding.UTF8.GetBytes("{\"exercise_history\":[{\"workout_id\":}]}")));
    var client = CreateClient(handler);

    var exception = (await FluentActions.Awaiting(() => client.GetExerciseHistoryWindowAsync(
        "template-1",
        new ExerciseHistoryQuery(0, 10),
        default)).Should().ThrowExactlyAsync<HevyException>()).Which;

    (exception.Code).Should().Be("unexpected_response");
    (handler.Requests).Should().ContainSingle();
  }

  [Theory]
  [InlineData("{\"exercise_history\":[]")]
  [InlineData("{\"exercise_history\":[]} trailing")]
  [InlineData("{\"exercise_history\":[]} []")]
  [InlineData("{\"exercise_history\":[],\"metadata\":[}")]
  public async Task HistoryWindowRejectsIncompleteOrTrailingEnvelopeContent(string payload)
  {
    var handler = HistoryHandler(new ChunkedReadStream(Encoding.UTF8.GetBytes(payload), 2));
    var client = CreateClient(handler);

    var exception = (await FluentActions.Awaiting(() => client.GetExerciseHistoryWindowAsync(
        "template-1",
        new ExerciseHistoryQuery(0, 10),
        default)).Should().ThrowExactlyAsync<HevyException>()).Which;

    (exception.Code).Should().Be("unexpected_response");
    (handler.Requests).Should().ContainSingle();
  }

  [Fact]
  public async Task HistoryWindowAcceptsUnknownMemberBeforeHistoryAcrossTokenBoundaries()
  {
    var payload = "{\"metadata\":{\"nested\":[true,{\"label\":\"before\"}]},\"exercise_history\":[" +
        Entry(1, "2026-07-01T00:00:00Z", null) + "]}";
    var handler = HistoryHandler(new ChunkedReadStream(Encoding.UTF8.GetBytes(payload), 3));
    var client = CreateClient(handler);

    var result = await client.GetExerciseHistoryWindowAsync(
        "template-1",
        new ExerciseHistoryQuery(0, 10),
        default);

    ((result.Items).Should().ContainSingle().Which.WorkoutId).Should().Be("workout-0001");
    (result.Truncated).Should().BeFalse();
    (result.ScannedItemCount).Should().Be(1);
  }

  [Fact]
  public async Task HistoryWindowAcceptsUnknownMemberAfterHistoryAcrossTokenBoundaries()
  {
    var payload = "{\"exercise_history\":[" + Entry(1, "2026-07-01T00:00:00Z", null) +
        "],\"metadata\":{\"nested\":[false,{\"label\":\"after\"}]}}   ";
    var handler = HistoryHandler(new ChunkedReadStream(Encoding.UTF8.GetBytes(payload), 1));
    var client = CreateClient(handler);

    var result = await client.GetExerciseHistoryWindowAsync(
        "template-1",
        new ExerciseHistoryQuery(0, 10),
        default);

    ((result.Items).Should().ContainSingle().Which.WorkoutId).Should().Be("workout-0001");
    (result.Truncated).Should().BeFalse();
    (result.ScannedItemCount).Should().Be(1);
  }

  [Fact]
  public async Task HistoryWindowRejectsMissingHistoryMember()
  {
    var handler = HistoryHandler(new ChunkedReadStream(Encoding.UTF8.GetBytes("{\"metadata\":{}}"), 1));
    var client = CreateClient(handler);

    var exception = (await FluentActions.Awaiting(() => client.GetExerciseHistoryWindowAsync(
        "template-1",
        new ExerciseHistoryQuery(0, 10),
        default)).Should().ThrowExactlyAsync<HevyException>()).Which;

    (exception.Code).Should().Be("unexpected_response");
  }

  [Fact]
  public async Task HistoryWindowRejectsDuplicateHistoryMembers()
  {
    var payload = "{\"exercise_history\":[],\"exercise_history\":[" +
        Entry(1, "2026-07-01T00:00:00Z", null) + "]}";
    var handler = HistoryHandler(new ChunkedReadStream(Encoding.UTF8.GetBytes(payload), 2));
    var client = CreateClient(handler);

    var exception = (await FluentActions.Awaiting(() => client.GetExerciseHistoryWindowAsync(
        "template-1",
        new ExerciseHistoryQuery(0, 10),
        default)).Should().ThrowExactlyAsync<HevyException>()).Which;

    (exception.Code).Should().Be("unexpected_response");
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

  private sealed class ChunkedReadStream(byte[] bytes, int maximumChunkSize) : MemoryStream(bytes)
  {
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        base.ReadAsync(buffer[..Math.Min(buffer.Length, maximumChunkSize)], cancellationToken);
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
