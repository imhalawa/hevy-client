using System;
using System.Collections.Immutable;

namespace Hevy.Core.UseCases;

public sealed record CreateWorkoutWrite(string Title, string? Description, DateTimeOffset StartTime, DateTimeOffset EndTime, bool IsPrivate, ImmutableList<CreateWorkoutExerciseWrite> Exercises);
