namespace Hevy.Mcp.Composite;

internal sealed record PageChunk<T>(ImmutableList<T> Items, bool More, int NextPage, int ScannedCapacity);
