using Hevy.Core.Models;

namespace Hevy.Mcp.Tools;

internal sealed record PageContinuation(int Page, int PageSize, string Detail);
