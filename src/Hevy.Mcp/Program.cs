using Hevy.Mcp.Configuration;
using Hevy.Mcp.Hosting;

namespace Hevy.Mcp;

public sealed class Program
{
  private Program()
  {
  }

  public static async Task<int> Main(string[] args)
  {
    try
    {
      var options = HevyMcpOptions.FromEnvironment();
      await (options.Transport switch
      {
        HevyMcpTransport.Stdio => StdioHost.RunAsync(args, options, CancellationToken.None),
        HevyMcpTransport.Http => HttpHost.RunAsync(args, options, CancellationToken.None),
        _ => throw new InvalidOperationException("The MCP transport is unsupported."),
      });

      return 0;
    }
    catch (InvalidOperationException exception)
    {
      await Console.Error.WriteLineAsync(exception.Message);
      return 2;
    }
  }
}
