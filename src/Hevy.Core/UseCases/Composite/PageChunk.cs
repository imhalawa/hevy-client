namespace Hevy.Core.UseCases;

internal sealed record PageChunk<T>(IReadOnlyList<T> Items, bool More, int NextPage, int ScannedCapacity);
