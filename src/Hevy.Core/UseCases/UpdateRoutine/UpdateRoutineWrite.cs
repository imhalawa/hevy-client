using System.Collections.Immutable;

namespace Hevy.Core.UseCases;

public sealed record UpdateRoutineWrite(string Title, string? Notes, ImmutableList<UpdateRoutineExerciseWrite> Exercises);
