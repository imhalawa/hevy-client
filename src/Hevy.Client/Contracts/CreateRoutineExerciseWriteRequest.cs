using System.Collections.Immutable;

namespace Hevy.Client.Contracts;

public sealed record CreateRoutineExerciseWriteRequest(string ExerciseTemplateId, long? SupersetId, int? RestSeconds, string? Notes, ImmutableList<CreateRoutineSetWriteRequest> Sets);
