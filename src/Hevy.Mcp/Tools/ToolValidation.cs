using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Protocol;
using Hevy.Client;
using Hevy.Core.Models;
using Hevy.Mcp.Caching;

namespace Hevy.Mcp.Tools;

internal static class ToolValidation
{
  internal static void Workout(CreateWorkoutWrite workout)
  {
    ArgumentNullException.ThrowIfNull(workout);
    Required(workout.Title, "workout title");
    if (workout.EndTime < workout.StartTime) throw new ArgumentException("Workout end time cannot be before its start time.", nameof(workout));
    ArgumentNullException.ThrowIfNull(workout.Exercises);
    foreach (var exercise in workout.Exercises)
    {
      ArgumentNullException.ThrowIfNull(exercise);
      Required(exercise.ExerciseTemplateId, "exercise template id");
      ArgumentNullException.ThrowIfNull(exercise.Sets);
      foreach (var set in exercise.Sets) ArgumentNullException.ThrowIfNull(set);
    }
  }

  internal static void Workout(UpdateWorkoutWrite workout)
  {
    ArgumentNullException.ThrowIfNull(workout);
    Required(workout.Title, "workout title");
    if (workout.EndTime < workout.StartTime) throw new ArgumentException("Workout end time cannot be before its start time.", nameof(workout));
    ArgumentNullException.ThrowIfNull(workout.Exercises);
    foreach (var exercise in workout.Exercises)
    {
      ArgumentNullException.ThrowIfNull(exercise);
      Required(exercise.ExerciseTemplateId, "exercise template id");
      ArgumentNullException.ThrowIfNull(exercise.Sets);
      foreach (var set in exercise.Sets) ArgumentNullException.ThrowIfNull(set);
    }
  }

  internal static void Routine(CreateRoutineWrite routine)
  {
    ArgumentNullException.ThrowIfNull(routine);
    Required(routine.Title, "routine title");
    ArgumentNullException.ThrowIfNull(routine.Exercises);
    foreach (var exercise in routine.Exercises)
    {
      ArgumentNullException.ThrowIfNull(exercise);
      Required(exercise.ExerciseTemplateId, "exercise template id");
      ArgumentNullException.ThrowIfNull(exercise.Sets);
      foreach (var set in exercise.Sets) ArgumentNullException.ThrowIfNull(set);
    }
  }

  internal static void Routine(UpdateRoutineWrite routine)
  {
    ArgumentNullException.ThrowIfNull(routine);
    Required(routine.Title, "routine title");
    ArgumentNullException.ThrowIfNull(routine.Exercises);
    foreach (var exercise in routine.Exercises)
    {
      ArgumentNullException.ThrowIfNull(exercise);
      Required(exercise.ExerciseTemplateId, "exercise template id");
      ArgumentNullException.ThrowIfNull(exercise.Sets);
      foreach (var set in exercise.Sets) ArgumentNullException.ThrowIfNull(set);
    }
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
    if (!force && expectedUpdatedAt is null)
    {
      throw new ArgumentException("expected_updated_at is required unless force is true.", nameof(expectedUpdatedAt));
    }
  }

  internal static void Required(string value, string field)
  {
    if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"A {field} is required.", field);
  }
}
