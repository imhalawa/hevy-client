using System.Collections.Immutable;

namespace Hevy.Client.Contracts;

public sealed record WorkoutExerciseWriteRequest(string ExerciseTemplateId, long? SupersetId, string? Notes, ImmutableList<WorkoutSetWriteRequest> Sets);
