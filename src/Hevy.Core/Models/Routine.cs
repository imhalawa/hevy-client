using System;
using System.Collections.Immutable;

namespace Hevy.Core.Models;

public sealed record Routine(string Id, string Title, long? FolderId, DateTimeOffset UpdatedAt, DateTimeOffset CreatedAt, ImmutableList<RoutineExercise> Exercises);
