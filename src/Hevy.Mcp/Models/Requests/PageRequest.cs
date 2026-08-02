namespace Hevy.Mcp.Tools;

internal sealed record PageRequest(int Page, int PageSize, int MaximumPageSize, string Detail)
{
  internal void Validate()
  {
    ArgumentOutOfRangeException.ThrowIfLessThan(Page, 1, nameof(Page));
    ArgumentOutOfRangeException.ThrowIfLessThan(PageSize, 1, nameof(PageSize));
    ArgumentOutOfRangeException.ThrowIfGreaterThan(PageSize, MaximumPageSize, nameof(PageSize));
    if (Detail is not ("compact" or "full")) throw new ArgumentException("Detail must be compact or full.", nameof(Detail));
  }
}
