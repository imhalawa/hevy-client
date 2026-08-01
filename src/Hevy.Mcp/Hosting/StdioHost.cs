using Hevy.Mcp.Configuration;
using Hevy.Mcp.Diagnostics;

namespace Hevy.Mcp.Hosting;

internal static class StdioHost
{
  internal static async Task RunAsync(string[] args, HevyMcpOptions options, CancellationToken cancellationToken)
  {

    var builder = Host.CreateApplicationBuilder(args);
    builder.Logging.ClearProviders();
    var diagnostics = RedactingLoggerProvider.Create(options, Console.Error);
    if (diagnostics is not null)
    {
      builder.Logging.AddProvider(diagnostics);
    }
    builder.Services.AddHevyMcpServer(options, diagnostics).WithStdioServerTransport();

    using var host = builder.Build();
    await host.RunAsync(cancellationToken);
  }
}
