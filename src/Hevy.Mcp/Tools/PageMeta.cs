using Hevy.Core.Models;

namespace Hevy.Mcp.Tools;

internal sealed record PageMeta<TContinuation>(
    int Page,
    int PageCount,
    int PageSize,
    string Detail,
    bool Truncated,
    TContinuation? Continuation = default)
    where TContinuation : class;
