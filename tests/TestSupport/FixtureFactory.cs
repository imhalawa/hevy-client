using AutoFixture;
using Hevy.Client.Models;

namespace TestSupport;

public static class FixtureFactory
{
  public static T Create<T>() => Customized().Create<T>();

  private static IFixture Customized()
  {
    var fixture = new AutoFixture.Fixture();
    var createWorkout = CreateWorkout();
    fixture.Inject(createWorkout);
    fixture.Inject(new UpdateWorkoutRequest(createWorkout.Workout));
    fixture.Inject(CreateRoutine());
    fixture.Inject(UpdateRoutine());
    fixture.Inject(new CreateRoutineFolderRequest(new RoutineFolderWrite("Push Pull")));
    fixture.Inject(CreateExerciseTemplate());
    fixture.Inject(CreateBodyMeasurement());
    fixture.Inject(UpdateBodyMeasurement());
    return fixture;
  }

  private static CreateWorkoutRequest CreateWorkout() => (CreateWorkoutRequest)new CreateWorkoutCommand(
      new CreateWorkoutWrite(
          "Friday Leg Day",
          "Sanitized workout",
          new DateTimeOffset(2024, 8, 14, 12, 0, 0, TimeSpan.Zero),
          new DateTimeOffset(2024, 8, 14, 12, 30, 0, TimeSpan.Zero),
          false,
          [new CreateWorkoutExerciseWrite("D04AC939", null, "Sanitized note", [new CreateWorkoutSetWrite(SetType.Normal, 100, 10, null, null, null, new WorkoutRpe(8.5m))])]));

  private static CreateRoutineRequest CreateRoutine() => (CreateRoutineRequest)new CreateRoutineCommand(
      new CreateRoutineWrite(
          "April Leg Day",
          null,
          "Sanitized routine",
          [new CreateRoutineExerciseWrite("D04AC939", null, 90, "Controlled", [new CreateRoutineSetWrite(SetType.Normal, 100, 10, null, null, null, new CreateRoutineRepRange(8, 12))])]));

  private static UpdateRoutineRequest UpdateRoutine() => (UpdateRoutineRequest)new UpdateRoutineCommand(
      new UpdateRoutineWrite(
          "April Leg Day",
          "Sanitized routine",
          [new UpdateRoutineExerciseWrite("D04AC939", null, 90, "Controlled", [new UpdateRoutineSetWrite(SetType.Normal, 100, 10, null, null, null, new RepRange(8, 12))])]));

  private static CreateExerciseTemplateRequest CreateExerciseTemplate() => (CreateExerciseTemplateRequest)new CreateExerciseTemplateCommand(
      new CustomExerciseWrite(
          "Bench Press",
          CustomExerciseType.WeightReps,
          EquipmentCategory.Barbell,
          MuscleGroup.Chest,
          [MuscleGroup.Triceps, MuscleGroup.Shoulders]));

  private static CreateBodyMeasurementRequest CreateBodyMeasurement() => (CreateBodyMeasurementRequest)new CreateBodyMeasurementCommand(new CreateBodyMeasurementWrite(
      new DateOnly(2024, 8, 14), 80.5m, 65m, 18.5m, 38m, 115m, 95m, 35m, 35.5m, 28m, 28.5m, 85m, 80m, 95m, 55m, 55.5m, 37m, 37.5m));

  private static UpdateBodyMeasurementRequest UpdateBodyMeasurement() => (UpdateBodyMeasurementRequest)new UpdateBodyMeasurementCommand(new UpdateBodyMeasurementWrite(
      80.5m, 65m, 18.5m, 38m, 115m, 95m, 35m, 35.5m, 28m, 28.5m, 85m, 80m, 95m, 55m, 55.5m, 37m, 37.5m));
}
