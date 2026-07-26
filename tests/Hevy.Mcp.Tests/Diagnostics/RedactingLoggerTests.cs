using System.Globalization;
using System.Text.Json;
using Hevy.Mcp.Configuration;
using Hevy.Mcp.Diagnostics;
using Microsoft.Extensions.Logging;
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

  private static HevyMcpOptions Options(string? logLevel)
  {
    var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
    {
      ["HEVY_API_KEY"] = "fixture-api-key-never-output",
      ["HEVY_LOG_LEVEL"] = logLevel,
    };
    return HevyMcpOptions.FromEnvironment(environment.GetValueOrDefault);
  }
}
