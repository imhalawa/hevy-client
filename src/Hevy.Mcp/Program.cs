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
      if (options.Transport is HevyMcpTransport.Stdio)
      {
        await StdioHost.RunAsync(args, options, CancellationToken.None);
      }
      else
      {
        await HttpHost.RunAsync(args, options, CancellationToken.None);
      }

      return 0;
    }
    catch (InvalidOperationException exception)
    {
      await Console.Error.WriteLineAsync(exception.Message);
      return 2;
    }
  }
}
