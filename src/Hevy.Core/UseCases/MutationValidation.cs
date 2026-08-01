namespace Hevy.Core.UseCases;

internal static class MutationValidation
{
  internal static void Workout(CreateWorkoutWrite workout)
  {
    ArgumentNullException.ThrowIfNull(workout);
    Required(workout.Title, "workout title");
    if (workout.EndTime < workout.StartTime) throw new ArgumentException("Workout end time cannot be before its start time.", nameof(workout));
    Exercises(workout.Exercises, static exercise => exercise.ExerciseTemplateId, static exercise => exercise.Sets);
  }

  internal static void Workout(UpdateWorkoutWrite workout)
  {
    ArgumentNullException.ThrowIfNull(workout);
    Required(workout.Title, "workout title");
    if (workout.EndTime < workout.StartTime) throw new ArgumentException("Workout end time cannot be before its start time.", nameof(workout));
    Exercises(workout.Exercises, static exercise => exercise.ExerciseTemplateId, static exercise => exercise.Sets);
  }

  internal static void Routine(CreateRoutineWrite routine)
  {
    ArgumentNullException.ThrowIfNull(routine);
    Required(routine.Title, "routine title");
    Exercises(routine.Exercises, static exercise => exercise.ExerciseTemplateId, static exercise => exercise.Sets);
  }

  internal static void Routine(UpdateRoutineWrite routine)
  {
    ArgumentNullException.ThrowIfNull(routine);
    Required(routine.Title, "routine title");
    Exercises(routine.Exercises, static exercise => exercise.ExerciseTemplateId, static exercise => exercise.Sets);
  }

  internal static void Exercise(CustomExerciseWrite exercise)
  {
    ArgumentNullException.ThrowIfNull(exercise);
    Required(exercise.Title, "exercise title");
    ArgumentNullException.ThrowIfNull(exercise.OtherMuscles);
    if (!Enum.IsDefined(exercise.ExerciseType) || !Enum.IsDefined(exercise.EquipmentCategory) ||
        !Enum.IsDefined(exercise.MuscleGroup) || exercise.OtherMuscles.Any(static muscle => !Enum.IsDefined(muscle)))
    {
      throw new ArgumentOutOfRangeException(nameof(exercise), "Exercise fields must use documented enum values.");
    }
  }

  internal static void Measurement(DateOnly date, params decimal?[] values)
  {
    if (date == DateOnly.MinValue) throw new ArgumentException("A measurement date is required.", nameof(date));
    if (values.Any(static value => value is < 0)) throw new ArgumentOutOfRangeException(nameof(values), "Measurement values cannot be negative.");
  }

  internal static void Guard(DateTimeOffset? expectedUpdatedAt, bool force)
  {
    if (!force && expectedUpdatedAt is null) throw new ArgumentException("expected_updated_at is required unless force is true.", nameof(expectedUpdatedAt));
  }

  internal static string Identifier(string value, string parameterName)
  {
    if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("An identifier is required.", parameterName);
    return value;
  }

  internal static void Required(string value, string field)
  {
    if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"A {field} is required.", field);
  }

  private static void Exercises<TExercise, TSet>(
      IEnumerable<TExercise> exercises,
      Func<TExercise, string> identifier,
      Func<TExercise, IEnumerable<TSet>> sets)
      where TExercise : class
      where TSet : class
  {
    ArgumentNullException.ThrowIfNull(exercises);
    foreach (var exercise in exercises)
    {
      ArgumentNullException.ThrowIfNull(exercise);
      Required(identifier(exercise), "exercise template id");
      var exerciseSets = sets(exercise);
      ArgumentNullException.ThrowIfNull(exerciseSets);
      foreach (var set in exerciseSets) ArgumentNullException.ThrowIfNull(set);
    }
  }
}
