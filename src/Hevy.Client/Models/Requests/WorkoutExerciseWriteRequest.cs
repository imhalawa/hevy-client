namespace Hevy.Client.Models;

public sealed record WorkoutExerciseWriteRequest(string ExerciseTemplateId, long? SupersetId, string? Notes, ImmutableList<WorkoutSetWriteRequest> Sets)
{
  internal static WorkoutExerciseWriteRequest From(CreateWorkoutExerciseWrite value) =>
      new(value.ExerciseTemplateId, value.SupersetId, value.Notes, value.Sets.Select(WorkoutSetWriteRequest.From).ToImmutableList());

  internal static WorkoutExerciseWriteRequest From(UpdateWorkoutExerciseWrite value) =>
      new(value.ExerciseTemplateId, value.SupersetId, value.Notes, value.Sets.Select(WorkoutSetWriteRequest.From).ToImmutableList());

  internal CreateWorkoutExerciseWrite ToCreateWorkout() =>
      new(ExerciseTemplateId, SupersetId, Notes, Sets.Select(static set => set.ToCreateWorkout()).ToImmutableList());

  internal UpdateWorkoutExerciseWrite ToUpdateWorkout() =>
      new(ExerciseTemplateId, SupersetId, Notes, Sets.Select(static set => set.ToUpdateWorkout()).ToImmutableList());
}
