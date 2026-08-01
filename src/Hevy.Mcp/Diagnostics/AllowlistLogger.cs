namespace Hevy.Mcp.Diagnostics;

internal sealed class AllowlistLogger(RedactingLoggerProvider provider) : ILogger
{
  public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

  public bool IsEnabled(LogLevel logLevel) => provider.IsEnabled(logLevel);

  public void Log<TState>(
      LogLevel logLevel,
      EventId eventId,
      TState state,
      Exception? exception,
      Func<TState, Exception?, string> formatter)
  {
    if (state is SafeOperationEvent operationEvent)
    {
      provider.Write(logLevel, operationEvent);
    }
  }
}
