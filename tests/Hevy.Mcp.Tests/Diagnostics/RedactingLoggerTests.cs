using System.Globalization;
using System.Text;
using System.Text.Json;
using Hevy.Client;
using Hevy.Mcp.Configuration;
using Hevy.Mcp.Diagnostics;
using Hevy.Mcp.Tests.Tools;
using Hevy.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TestSupport;
using Xunit;

namespace Hevy.Mcp.Tests.Diagnostics;

public sealed class RedactingLoggerTests
{
  private const string UnsafeContent = "fixture-key api-key: secret ?page=2 Private Workout 2026-07-25T10:00:00Z response_body=oops weight_kg=91.2";

  [Fact]
  public void TypedEventWritesOnlyAllowlistedFieldsAndIgnoresExceptionAndFormatter()
  {
    var writer = new StringWriter(CultureInfo.InvariantCulture);
    var options = Options("Information");
    using var provider = Assert.IsType<RedactingLoggerProvider>(RedactingLoggerProvider.Create(options, writer));
    var logger = provider.CreateLogger("unsafe-category-name");
    var correlationId = Guid.ParseExact("00112233445566778899aabbccddeeff", "N");
    var safeEvent = new SafeOperationEvent(
        DiagnosticOperationCategory.Read,
        DiagnosticDurationBucket.UnderOneSecond,
        DiagnosticOperationStatus.Failed,
        correlationId,
        DiagnosticExceptionCategory.Upstream,
        503);

    logger.Log(
        LogLevel.Warning,
        new EventId(8),
        safeEvent,
        new InvalidOperationException(UnsafeContent),
        static (_, _) => UnsafeContent);

    var line = Assert.Single(writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
    using var document = JsonDocument.Parse(line);
    var root = document.RootElement;
    Assert.Equal("read", root.GetProperty("operation_category").GetString());
    Assert.Equal("under_one_second", root.GetProperty("duration_bucket").GetString());
    Assert.Equal("failed", root.GetProperty("status").GetString());
    Assert.Equal(correlationId.ToString("N"), root.GetProperty("correlation_id").GetString());
    Assert.Equal("upstream", root.GetProperty("exception_category").GetString());
    Assert.Equal(503, root.GetProperty("http_status").GetInt32());
    Assert.Equal("stdio", root.GetProperty("transport").GetString());
    Assert.False(root.GetProperty("read_only").GetBoolean());
    Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("server_version").GetString()));
    Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("runtime_version").GetString()));
    Assert.DoesNotContain(UnsafeContent, line, StringComparison.Ordinal);
    Assert.DoesNotContain("unsafe-category-name", line, StringComparison.Ordinal);
    Assert.DoesNotContain("api-key", line, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("private workout", line, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("2026-07-25", line, StringComparison.Ordinal);
    Assert.DoesNotContain("91.2", line, StringComparison.Ordinal);
  }

  [Fact]
  public void ArbitraryLoggerStateIsRejectedInsteadOfScrubbed()
  {
    var writer = new StringWriter(CultureInfo.InvariantCulture);
    using var provider = Assert.IsType<RedactingLoggerProvider>(RedactingLoggerProvider.Create(Options("Trace"), writer));
    var logger = provider.CreateLogger("category");
    var arbitraryState = new Dictionary<string, object?>
    {
      ["api-key"] = "fixture-key",
      ["Authorization"] = "Bearer fixture-token",
      ["url"] = "https://api.hevyapp.com/v1/workouts?page=2",
      ["title"] = "Private Workout",
      ["timestamp"] = "2026-07-25T10:00:00Z",
      ["body"] = "upstream response body",
      ["weight_kg"] = 91.2m,
    };

    logger.Log(LogLevel.Error, new EventId(9), arbitraryState, new Exception(UnsafeContent), static (_, _) => UnsafeContent);

    Assert.Equal(string.Empty, writer.ToString());
  }

  [Fact]
  public void NoneLogLevelCreatesNoProviderAndEmitsNothing()
  {
    var writer = new StringWriter(CultureInfo.InvariantCulture);

    var provider = RedactingLoggerProvider.Create(Options(logLevel: null), writer);

    Assert.Null(provider);
    Assert.Equal(string.Empty, writer.ToString());
  }

  [Fact]
  public async Task ThrowingDiagnosticSinkCannotChangeACompletedMutationResult()
  {
    var writer = new ThrowingWriter();
    using var provider = Assert.IsType<RedactingLoggerProvider>(RedactingLoggerProvider.Create(Options("Information"), writer));
    var client = new FakeHevyClient();
    using var services = new ServiceCollection().AddSingleton<IHevyClient>(client).BuildServiceProvider();

    var result = await DiagnosticToolDispatch.InvokeAsync(
        cancellationToken => WorkoutWriteTools.CreateWorkout(
            services,
            FixtureFactory.CreateWorkoutRequest(),
            dry_run: false,
            cancellationToken),
        DiagnosticOperationCategory.Mutation,
        provider,
        CancellationToken.None);

    Assert.False(result.IsError);
    Assert.Equal(1, client.CallCount);
    Assert.Equal(nameof(IHevyClient.CreateWorkoutAsync), client.LastOperation);
    Assert.Equal(1, writer.WriteAttempts);
  }

  [Fact]
  public async Task ThrowingDiagnosticSinkCannotChangeStructuredErrorOrCancellationSemantics()
  {
    var errorWriter = new ThrowingWriter();
    using var errorProvider = Assert.IsType<RedactingLoggerProvider>(RedactingLoggerProvider.Create(Options("Information"), errorWriter));
    var expected = ToolExceptionFilter.Validation("Safe fixed validation message.");

    var actual = await DiagnosticToolDispatch.InvokeAsync(
        _ => Task.FromResult(expected),
        DiagnosticOperationCategory.Read,
        errorProvider,
        CancellationToken.None);

    Assert.Same(expected, actual);
    Assert.True(actual.IsError);
    Assert.Equal("validation_error", actual.Structured().GetProperty("error").GetProperty("code").GetString());
    Assert.Equal(1, errorWriter.WriteAttempts);

    var cancellationWriter = new ThrowingWriter();
    using var cancellationProvider = Assert.IsType<RedactingLoggerProvider>(RedactingLoggerProvider.Create(Options("Information"), cancellationWriter));
    using var source = new CancellationTokenSource();
    source.Cancel();

    var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DiagnosticToolDispatch.InvokeAsync(
        _ => Task.FromCanceled<ModelContextProtocol.Protocol.CallToolResult>(source.Token),
        DiagnosticOperationCategory.Read,
        cancellationProvider,
        source.Token));

    Assert.Equal(source.Token, exception.CancellationToken);
    Assert.Equal(1, cancellationWriter.WriteAttempts);
  }

  [Fact]
  public void FailedDiagnosticSinkIsDisabledAfterItsFirstException()
  {
    var writer = new ThrowingWriter();
    using var provider = Assert.IsType<RedactingLoggerProvider>(RedactingLoggerProvider.Create(Options("Trace"), writer));
    var operationEvent = new SafeOperationEvent(
        DiagnosticOperationCategory.Read,
        DiagnosticDurationBucket.UnderOneSecond,
        DiagnosticOperationStatus.Succeeded,
        Guid.ParseExact("00112233445566778899aabbccddeeff", "N"),
        DiagnosticExceptionCategory.None);

    provider.Write(LogLevel.Information, operationEvent);
    provider.Write(LogLevel.Information, operationEvent);

    Assert.Equal(1, writer.WriteAttempts);
  }

  [Fact]
  public void ClientValidationCodeIsARejectedValidationDiagnostic()
  {
    var result = ToolResults.Error(new ToolError(
        "validation",
        "Safe fixed validation message.",
        false,
        "00112233445566778899aabbccddeeff"));

    var operationEvent = SafeOperationEvent.FromToolResult(
        DiagnosticOperationCategory.Mutation,
        TimeSpan.FromMilliseconds(5),
        result,
        Guid.NewGuid());

    Assert.Equal(DiagnosticOperationStatus.Rejected, operationEvent.Status);
    Assert.Equal(DiagnosticExceptionCategory.Validation, operationEvent.ExceptionCategory);
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
