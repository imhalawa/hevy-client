namespace Hevy.Mcp.Composite;

internal sealed record UtcRange(int Weeks, DateTimeOffset Start, DateTimeOffset End);
