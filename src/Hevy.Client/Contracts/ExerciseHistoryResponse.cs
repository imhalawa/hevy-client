using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Hevy.Client.Contracts;

public sealed record ExerciseHistoryResponse([property: JsonRequired] ImmutableList<ExerciseHistoryEntryResponse> ExerciseHistory);
