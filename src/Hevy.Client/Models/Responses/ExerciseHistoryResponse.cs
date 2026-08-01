using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Hevy.Client.Models;

public sealed record ExerciseHistoryResponse([property: JsonRequired] ImmutableList<ExerciseHistoryEntryResponse> ExerciseHistory);
