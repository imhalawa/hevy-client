using System.Globalization;
using System.Text;
using System.Text.Json;
using Hevy.Mcp.Configuration;
using Hevy.Mcp.Diagnostics;
using Hevy.Mcp.Tests.Tools;
using Hevy.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TestSupport;
using Xunit;

namespace Hevy.Mcp.Tests.Diagnostics;

public sealed class DiagnosticSinkTests
{
  private const string UnsafeContent = "fixture-key api-key: secret ?page=2 Private Workout 2026-07-25T10:00:00Z response_body=oops weight_kg=91.2";

  [Fact]
  public void TypedEventWritesOnlyAllowlistedFieldsAndIgnoresExceptionAndFormatter()
  {
    var writer = new StringWriter(CultureInfo.InvariantCulture);
    var options = Options("Information");
    var provider = (DiagnosticSink.Create(options, writer)).Should().BeOfType<DiagnosticSink>().Which;
    var correlationId = Guid.ParseExact("00112233445566778899aabbccddeeff", "N");
    var safeEvent = new SafeOperationEvent(
        DiagnosticOperationCategory.Read,
        DiagnosticDurationBucket.UnderOneSecond,
        DiagnosticOperationStatus.Failed,
        correlationId,
        DiagnosticExceptionCategory.Upstream,
        503,
        "get_workouts",
        "hevy-request-123");

    provider.Write(LogLevel.Warning, safeEvent);

    var line = (writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)).Should().ContainSingle().Which;
    using var document = JsonDocument.Parse(line);
    var root = document.RootElement;
    (root.GetProperty("operation_category").GetString()).Should().Be("read");
    (root.GetProperty("operation_name").GetString()).Should().Be("get_workouts");
    (root.GetProperty("hevy_request_id").GetString()).Should().Be("hevy-request-123");
    (root.GetProperty("duration_bucket").GetString()).Should().Be("under_one_second");
    (root.GetProperty("status").GetString()).Should().Be("failed");
    (root.GetProperty("correlation_id").GetString()).Should().Be(correlationId.ToString("N"));
    (root.GetProperty("exception_category").GetString()).Should().Be("upstream");
    (root.GetProperty("http_status").GetInt32()).Should().Be(503);
    (root.GetProperty("transport").GetString()).Should().Be("stdio");
    (root.GetProperty("read_only").GetBoolean()).Should().BeFalse();
    (string.IsNullOrWhiteSpace(root.GetProperty("server_version").GetString())).Should().BeFalse();
    (string.IsNullOrWhiteSpace(root.GetProperty("runtime_version").GetString())).Should().BeFalse();
    (line).Should().NotContain(UnsafeContent);
    (line).Should().NotContain("unsafe-category-name");
    (line).Should().NotContainEquivalentOf("api-key");
    (line).Should().NotContainEquivalentOf("private workout");
    (line).Should().NotContain("2026-07-25");
    (line).Should().NotContain("91.2");
  }

  [Fact]
  public void NoneLogLevelCreatesNoProviderAndEmitsNothing()
  {
    var writer = new StringWriter(CultureInfo.InvariantCulture);

    var provider = DiagnosticSink.Create(Options(logLevel: null), writer);

    (provider).Should().BeNull();
    (writer.ToString()).Should().Be(string.Empty);
  }

  [Fact]
  public async Task ThrowingDiagnosticSinkCannotChangeACompletedMutationResult()
  {
    var writer = new ThrowingWriter();
    var provider = (DiagnosticSink.Create(Options("Information"), writer)).Should().BeOfType<DiagnosticSink>().Which;
    var client = new FakeHevyClient();
    using var services = new ServiceCollection().AddSingleton<IHevyClient>(client).BuildServiceProvider();

    var result = await DiagnosticToolDispatch.InvokeAsync(
        cancellationToken => WorkoutWriteTools.CreateWorkout(
            services,
            FixtureFactory.Create<CreateWorkoutCommand>(),
            dry_run: false,
            cancellationToken),
        DiagnosticOperationCategory.Mutation,
        provider,
        CancellationToken.None);

    (result.IsError).Should().BeFalse();
    (client.CallCount).Should().Be(1);
    (client.LastOperation).Should().Be(nameof(IHevyClient.CreateWorkoutAsync));
    (writer.WriteAttempts).Should().Be(1);
  }

  [Fact]
  public async Task ThrowingDiagnosticSinkCannotChangeStructuredErrorOrCancellationSemantics()
  {
    var errorWriter = new ThrowingWriter();
    var errorProvider = (DiagnosticSink.Create(Options("Information"), errorWriter)).Should().BeOfType<DiagnosticSink>().Which;
    var expected = ToolExceptionFilter.Validation("Safe fixed validation message.");

    var actual = await DiagnosticToolDispatch.InvokeAsync(
        _ => Task.FromResult(expected),
        DiagnosticOperationCategory.Read,
        errorProvider,
        CancellationToken.None);

    (actual).Should().BeSameAs(expected);
    (actual.IsError).Should().BeTrue();
    (actual.Structured().GetProperty("error").GetProperty("code").GetString()).Should().Be("validation_error");
    (errorWriter.WriteAttempts).Should().Be(1);

    var cancellationWriter = new ThrowingWriter();
    var cancellationProvider = (DiagnosticSink.Create(Options("Information"), cancellationWriter)).Should().BeOfType<DiagnosticSink>().Which;
    using var source = new CancellationTokenSource();
    source.Cancel();

    var exception = (await FluentActions.Awaiting(() => DiagnosticToolDispatch.InvokeAsync(
        _ => Task.FromCanceled<ModelContextProtocol.Protocol.CallToolResult>(source.Token),
        DiagnosticOperationCategory.Read,
        cancellationProvider,
        source.Token)).Should().ThrowAsync<OperationCanceledException>()).Which;

    (exception.CancellationToken).Should().Be(source.Token);
    (cancellationWriter.WriteAttempts).Should().Be(1);
  }

  [Fact]
  public void FailedDiagnosticSinkIsDisabledAfterItsFirstException()
  {
    var writer = new ThrowingWriter();
    var provider = (DiagnosticSink.Create(Options("Trace"), writer)).Should().BeOfType<DiagnosticSink>().Which;
    var operationEvent = new SafeOperationEvent(
        DiagnosticOperationCategory.Read,
        DiagnosticDurationBucket.UnderOneSecond,
        DiagnosticOperationStatus.Succeeded,
        Guid.ParseExact("00112233445566778899aabbccddeeff", "N"),
        DiagnosticExceptionCategory.None);

    provider.Write(LogLevel.Information, operationEvent);
    provider.Write(LogLevel.Information, operationEvent);

    (writer.WriteAttempts).Should().Be(1);
  }

  [Fact]
  public void ClientValidationCodeIsARejectedValidationDiagnostic()
  {
    var result = ToolResults.Error(new ToolError(
        "validation",
        "Safe fixed validation message.",
        false,
        "00112233445566778899aabbccddeeff",
        400,
        "hevy-request-123"));

    var operationEvent = SafeOperationEvent.FromToolResult(
        DiagnosticOperationCategory.Mutation,
        TimeSpan.FromMilliseconds(5),
        result,
        Guid.NewGuid(),
        "create_workout");

    (operationEvent.Status).Should().Be(DiagnosticOperationStatus.Rejected);
    (operationEvent.ExceptionCategory).Should().Be(DiagnosticExceptionCategory.Validation);
    (operationEvent.OperationName).Should().Be("create_workout");
    (operationEvent.HevyRequestId).Should().Be("hevy-request-123");
  }

  private static HevyMcpOptions Options(string? logLevel)
  {
    var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
    {
      ["HEVY_API_KEY"] = "fixture-api-key-never-output",
      ["HEVY_LOG_LEVEL"] = logLevel,
    };
    return HevyMcpOptions.FromEnvironment(environment.GetValueOrDefault);
  }

  private sealed class ThrowingWriter : TextWriter
  {
    public override Encoding Encoding => Encoding.UTF8;

    internal int WriteAttempts { get; private set; }

    public override void WriteLine(string? value)
    {
      WriteAttempts++;
      throw new IOException("Deliberate diagnostic sink failure containing private-looking text.");
    }
  }
}
