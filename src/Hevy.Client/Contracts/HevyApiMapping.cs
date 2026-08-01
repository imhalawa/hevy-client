using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using Hevy.Core.Models;

namespace Hevy.Client.Contracts;

internal static class HevyApiMapping
{
  internal static Workout ToDomain(this WorkoutResponse value)
  {
    return new Workout(value.Id, value.Title, value.RoutineId, value.Description, value.StartTime, value.EndTime, value.UpdatedAt, value.CreatedAt, value.Exercises.Select(ToDomain).ToImmutableList());
  }

  internal static WorkoutExercise ToDomain(this WorkoutExerciseResponse value)
  {
    return new WorkoutExercise(value.Index, value.Title, value.Notes, value.ExerciseTemplateId, value.SupersetId, value.Sets.Select(ToDomain).ToImmutableList());
  }

  internal static WorkoutSet ToDomain(this WorkoutSetResponse value)
  {
    return new WorkoutSet(value.Index, value.Type, value.WeightKg, value.Reps, value.DistanceMeters, value.DurationSeconds, value.Rpe, value.CustomMetric);
  }

  internal static WorkoutEvent ToDomain(this WorkoutEventResponse value)
  {
    if (!(value is UpdatedWorkoutEventResponse updatedWorkoutEventResponse))
    {
      if (value is DeletedWorkoutEventResponse deletedWorkoutEventResponse)
      {
        return new DeletedWorkoutEvent(deletedWorkoutEventResponse.Id, deletedWorkoutEventResponse.DeletedAt);
      }
      throw new InvalidOperationException("Unsupported workout event response.");
    }
    return new UpdatedWorkoutEvent(updatedWorkoutEventResponse.Workout.ToDomain());
  }

  internal static Routine ToDomain(this RoutineResponse value)
  {
    return new Routine(value.Id, value.Title, value.FolderId, value.UpdatedAt, value.CreatedAt, value.Exercises.Select(ToDomain).ToImmutableList());
  }

  internal static RoutineExercise ToDomain(this RoutineExerciseResponse value)
  {
    return new RoutineExercise(value.Index, value.Title, value.RestSeconds, value.Notes, value.ExerciseTemplateId, value.SupersetId, value.Sets.Select(ToDomain).ToImmutableList());
  }

  internal static RoutineSet ToDomain(this RoutineSetResponse value)
  {
    return new RoutineSet(value.Index, value.Type, value.WeightKg, value.Reps, value.DistanceMeters, value.DurationSeconds, value.Rpe, value.CustomMetric, value.RepRange);
  }

  internal static RoutineFolder ToDomain(this RoutineFolderResponse value)
  {
    return new RoutineFolder(value.Id, value.Index, value.Title, value.UpdatedAt, value.CreatedAt);
  }

  internal static ExerciseTemplate ToDomain(this ExerciseTemplateResponse value)
  {
    return new ExerciseTemplate(value.Id, value.Title, value.Type, value.PrimaryMuscleGroup, value.SecondaryMuscleGroups, (EquipmentCategory)value.EquipmentCategory, value.IsCustom);
  }

  internal static ExerciseHistoryEntry ToDomain(this ExerciseHistoryEntryResponse value)
  {
    return new ExerciseHistoryEntry(value.WorkoutId, value.WorkoutTitle, value.WorkoutStartTime, value.WorkoutEndTime, value.ExerciseTemplateId, value.WeightKg, value.Reps, value.DistanceMeters, value.DurationSeconds, value.Rpe, value.CustomMetric, value.SetType);
  }

  internal static BodyMeasurement ToDomain(this BodyMeasurementResponse value)
  {
    return new BodyMeasurement(value.Date, value.WeightKg, value.LeanMassKg, value.FatPercent, value.NeckCm, value.ShoulderCm, value.ChestCm, value.LeftBicepCm, value.RightBicepCm, value.LeftForearmCm, value.RightForearmCm, value.Abdomen, value.Waist, value.Hips, value.LeftThigh, value.RightThigh, value.LeftCalf, value.RightCalf);
  }

  internal static UserInfo ToDomain(this UserInfoDataResponse value)
  {
    return new UserInfo(value.Id, value.Name, value.Url);
  }

  internal static WorkoutWriteRequest ToRequest(this CreateWorkoutWrite value)
  {
    return new WorkoutWriteRequest(value.Title, value.Description, value.StartTime, value.EndTime, value.IsPrivate, value.Exercises.Select(ToRequest).ToImmutableList());
  }

  private static WorkoutExerciseWriteRequest ToRequest(this CreateWorkoutExerciseWrite value)
  {
    return new WorkoutExerciseWriteRequest(value.ExerciseTemplateId, value.SupersetId, value.Notes, value.Sets.Select(ToRequest).ToImmutableList());
  }

  internal static WorkoutSetWriteRequest ToRequest(this CreateWorkoutSetWrite value)
  {
    WorkoutRpe? rpe = value.Rpe;
    if (rpe.HasValue && !WorkoutRpe.IsValid(rpe.GetValueOrDefault().Value))
    {
      throw new JsonException("RPE is invalid.");
    }
    return new WorkoutSetWriteRequest((SetTypeApi)value.Type, value.WeightKg, value.Reps, value.DistanceMeters, value.DurationSeconds, value.CustomMetric, value.Rpe);
  }

  internal static WorkoutWriteRequest ToRequest(this UpdateWorkoutWrite value)
  {
    return new WorkoutWriteRequest(value.Title, value.Description, value.StartTime, value.EndTime, value.IsPrivate, value.Exercises.Select(ToRequest).ToImmutableList());
  }

  private static WorkoutExerciseWriteRequest ToRequest(this UpdateWorkoutExerciseWrite value)
  {
    return new WorkoutExerciseWriteRequest(value.ExerciseTemplateId, value.SupersetId, value.Notes, value.Sets.Select(ToRequest).ToImmutableList());
  }

  internal static WorkoutSetWriteRequest ToRequest(this UpdateWorkoutSetWrite value)
  {
    WorkoutRpe? rpe = value.Rpe;
    if (rpe.HasValue && !WorkoutRpe.IsValid(rpe.GetValueOrDefault().Value))
    {
      throw new JsonException("RPE is invalid.");
    }
    return new WorkoutSetWriteRequest((SetTypeApi)value.Type, value.WeightKg, value.Reps, value.DistanceMeters, value.DurationSeconds, value.CustomMetric, value.Rpe);
  }

  internal static CreateRoutineWriteRequest ToRequest(this CreateRoutineWrite value)
  {
    return new CreateRoutineWriteRequest(value.Title, value.FolderId, value.Notes, value.Exercises.Select(ToRequest).ToImmutableList());
  }

  internal static UpdateRoutineWriteRequest ToRequest(this UpdateRoutineWrite value)
  {
    return new UpdateRoutineWriteRequest(value.Title, value.Notes, value.Exercises.Select(ToRequest).ToImmutableList());
  }

  private static CreateRoutineExerciseWriteRequest ToRequest(this CreateRoutineExerciseWrite value)
  {
    return new CreateRoutineExerciseWriteRequest(value.ExerciseTemplateId, value.SupersetId, value.RestSeconds, value.Notes, value.Sets.Select(ToRequest).ToImmutableList());
  }

  private static UpdateRoutineExerciseWriteRequest ToRequest(this UpdateRoutineExerciseWrite value)
  {
    return new UpdateRoutineExerciseWriteRequest(value.ExerciseTemplateId, value.SupersetId, value.RestSeconds, value.Notes, value.Sets.Select(ToRequest).ToImmutableList());
  }

  private static CreateRoutineSetWriteRequest ToRequest(this CreateRoutineSetWrite value)
  {
    return new CreateRoutineSetWriteRequest((SetTypeApi)value.Type, value.WeightKg, value.Reps, value.DistanceMeters, value.DurationSeconds, value.CustomMetric, value.RepRange);
  }

  private static UpdateRoutineSetWriteRequest ToRequest(this UpdateRoutineSetWrite value)
  {
    return new UpdateRoutineSetWriteRequest((SetTypeApi)value.Type, value.WeightKg, value.Reps, value.DistanceMeters, value.DurationSeconds, value.CustomMetric, value.RepRange);
  }

  internal static CustomExerciseWriteRequest ToRequest(this CustomExerciseWrite value)
  {
    return new CustomExerciseWriteRequest(value.Title, (CustomExerciseTypeApi)value.ExerciseType, (EquipmentCategoryApi)value.EquipmentCategory, (MuscleGroupApi)value.MuscleGroup, value.OtherMuscles.Select((MuscleGroup muscle) => (MuscleGroupApi)muscle).ToImmutableList());
  }

  internal static CreateBodyMeasurementRequest ToRequest(this NewBodyMeasurement value)
  {
    return new CreateBodyMeasurementRequest(value.Date, value.WeightKg, value.LeanMassKg, value.FatPercent, value.NeckCm, value.ShoulderCm, value.ChestCm, value.LeftBicepCm, value.RightBicepCm, value.LeftForearmCm, value.RightForearmCm, value.Abdomen, value.Waist, value.Hips, value.LeftThigh, value.RightThigh, value.LeftCalf, value.RightCalf);
  }

  internal static UpdateBodyMeasurementRequest ToRequest(this BodyMeasurementUpdate value)
  {
    return new UpdateBodyMeasurementRequest(value.WeightKg, value.LeanMassKg, value.FatPercent, value.NeckCm, value.ShoulderCm, value.ChestCm, value.LeftBicepCm, value.RightBicepCm, value.LeftForearmCm, value.RightForearmCm, value.Abdomen, value.Waist, value.Hips, value.LeftThigh, value.RightThigh, value.LeftCalf, value.RightCalf);
  }

  internal static CreateWorkoutCommand ToCommand(this CreateWorkoutRequest value)
  {
    return new CreateWorkoutCommand(value.Workout.ToCreateWorkout());
  }

  internal static UpdateWorkoutCommand ToCommand(this UpdateWorkoutRequest value)
  {
    return new UpdateWorkoutCommand(value.Workout.ToUpdateWorkout());
  }

  private static CreateWorkoutWrite ToCreateWorkout(this WorkoutWriteRequest value)
  {
    return new CreateWorkoutWrite(value.Title, value.Description, value.StartTime, value.EndTime, value.IsPrivate, value.Exercises.Select(ToCreateWorkout).ToImmutableList());
  }

  private static CreateWorkoutExerciseWrite ToCreateWorkout(this WorkoutExerciseWriteRequest value)
  {
    return new CreateWorkoutExerciseWrite(value.ExerciseTemplateId, value.SupersetId, value.Notes, value.Sets.Select(ToCreateWorkout).ToImmutableList());
  }

  private static CreateWorkoutSetWrite ToCreateWorkout(this WorkoutSetWriteRequest value)
  {
    return new CreateWorkoutSetWrite((SetType)value.Type, value.WeightKg, value.Reps, value.DistanceMeters, value.DurationSeconds, value.CustomMetric, value.Rpe);
  }

  private static UpdateWorkoutWrite ToUpdateWorkout(this WorkoutWriteRequest value)
  {
    return new UpdateWorkoutWrite(value.Title, value.Description, value.StartTime, value.EndTime, value.IsPrivate, value.Exercises.Select(ToUpdateWorkout).ToImmutableList());
  }

  private static UpdateWorkoutExerciseWrite ToUpdateWorkout(this WorkoutExerciseWriteRequest value)
  {
    return new UpdateWorkoutExerciseWrite(value.ExerciseTemplateId, value.SupersetId, value.Notes, value.Sets.Select(ToUpdateWorkout).ToImmutableList());
  }

  private static UpdateWorkoutSetWrite ToUpdateWorkout(this WorkoutSetWriteRequest value)
  {
    return new UpdateWorkoutSetWrite((SetType)value.Type, value.WeightKg, value.Reps, value.DistanceMeters, value.DurationSeconds, value.CustomMetric, value.Rpe);
  }

  internal static CreateRoutineCommand ToCommand(this CreateRoutineRequest value)
  {
    return new CreateRoutineCommand(value.Routine.ToDomain());
  }

  internal static UpdateRoutineCommand ToCommand(this UpdateRoutineRequest value)
  {
    return new UpdateRoutineCommand(value.Routine.ToDomain());
  }

  private static CreateRoutineWrite ToDomain(this CreateRoutineWriteRequest value)
  {
    return new CreateRoutineWrite(value.Title, value.FolderId, value.Notes, value.Exercises.Select(ToDomain).ToImmutableList());
  }

  private static UpdateRoutineWrite ToDomain(this UpdateRoutineWriteRequest value)
  {
    return new UpdateRoutineWrite(value.Title, value.Notes, value.Exercises.Select(ToDomain).ToImmutableList());
  }

  private static CreateRoutineExerciseWrite ToDomain(this CreateRoutineExerciseWriteRequest value)
  {
    return new CreateRoutineExerciseWrite(value.ExerciseTemplateId, value.SupersetId, value.RestSeconds, value.Notes, value.Sets.Select(ToDomain).ToImmutableList());
  }

  private static UpdateRoutineExerciseWrite ToDomain(this UpdateRoutineExerciseWriteRequest value)
  {
    return new UpdateRoutineExerciseWrite(value.ExerciseTemplateId, value.SupersetId, value.RestSeconds, value.Notes, value.Sets.Select(ToDomain).ToImmutableList());
  }

  private static CreateRoutineSetWrite ToDomain(this CreateRoutineSetWriteRequest value)
  {
    return new CreateRoutineSetWrite((SetType)value.Type, value.WeightKg, value.Reps, value.DistanceMeters, value.DurationSeconds, value.CustomMetric, value.RepRange);
  }

  private static UpdateRoutineSetWrite ToDomain(this UpdateRoutineSetWriteRequest value)
  {
    return new UpdateRoutineSetWrite((SetType)value.Type, value.WeightKg, value.Reps, value.DistanceMeters, value.DurationSeconds, value.CustomMetric, value.RepRange);
  }

  internal static CreateRoutineFolderCommand ToCommand(this CreateRoutineFolderRequest value)
  {
    return new CreateRoutineFolderCommand(value.RoutineFolder);
  }

  internal static CreateExerciseTemplateCommand ToCommand(this CreateExerciseTemplateRequest value)
  {
    return new CreateExerciseTemplateCommand(value.Exercise.ToDomain());
  }

  private static CustomExerciseWrite ToDomain(this CustomExerciseWriteRequest value)
  {
    return new CustomExerciseWrite(value.Title, (CustomExerciseType)value.ExerciseType, (EquipmentCategory)value.EquipmentCategory, (MuscleGroup)value.MuscleGroup, value.OtherMuscles.Select((MuscleGroupApi muscle) => (MuscleGroup)muscle).ToImmutableList());
  }

  internal static NewBodyMeasurement ToCommand(this CreateBodyMeasurementRequest value)
  {
    return new NewBodyMeasurement(value.Date, value.WeightKg, value.LeanMassKg, value.FatPercent, value.NeckCm, value.ShoulderCm, value.ChestCm, value.LeftBicepCm, value.RightBicepCm, value.LeftForearmCm, value.RightForearmCm, value.Abdomen, value.Waist, value.Hips, value.LeftThigh, value.RightThigh, value.LeftCalf, value.RightCalf);
  }

  internal static BodyMeasurementUpdate ToCommand(this UpdateBodyMeasurementRequest value)
  {
    return new BodyMeasurementUpdate(value.WeightKg, value.LeanMassKg, value.FatPercent, value.NeckCm, value.ShoulderCm, value.ChestCm, value.LeftBicepCm, value.RightBicepCm, value.LeftForearmCm, value.RightForearmCm, value.Abdomen, value.Waist, value.Hips, value.LeftThigh, value.RightThigh, value.LeftCalf, value.RightCalf);
  }
}
