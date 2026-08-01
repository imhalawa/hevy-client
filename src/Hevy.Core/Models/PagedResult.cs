namespace Hevy.Core.Models;

public sealed record PagedResult<T>(int Page, int PageCount, ImmutableList<T> Items);
