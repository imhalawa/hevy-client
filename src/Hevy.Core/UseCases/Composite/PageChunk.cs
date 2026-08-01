namespace Hevy.Core.UseCases;

internal sealed record PageChunk<T>(ImmutableList<T> Items, bool More, int NextPage, int ScannedCapacity);
