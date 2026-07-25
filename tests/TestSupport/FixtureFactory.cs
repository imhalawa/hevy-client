using Hevy.Client.Models;

namespace TestSupport;

public static class FixtureFactory
{
    public static CreateWorkoutRequest CreateWorkoutRequest() => new(
        new WorkoutWrite(
            "Friday Leg Day",
            "Sanitized workout",
            new DateTimeOffset(2024, 8, 14, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2024, 8, 14, 12, 30, 0, TimeSpan.Zero),
            false,
            [new WorkoutExerciseWrite("D04AC939", null, "Sanitized note", [new WorkoutSetWrite(SetType.Normal, 100, 10, null, null, null, new WorkoutRpe(8.5m))])]));

    public static UpdateWorkoutRequest UpdateWorkoutRequest() => new(CreateWorkoutRequest().Workout);

    public static CreateRoutineRequest CreateRoutineRequest() => new(
        new CreateRoutineWrite(
            "April Leg Day",
            null,
            "Sanitized routine",
            [new CreateRoutineExerciseWrite("D04AC939", null, 90, "Controlled", [new CreateRoutineSetWrite(SetType.Normal, 100, 10, null, null, null, new CreateRoutineRepRange(8, 12))])]));

    public static UpdateRoutineRequest UpdateRoutineRequest() => new(
        new UpdateRoutineWrite(
            "April Leg Day",
            "Sanitized routine",
            [new UpdateRoutineExerciseWrite("D04AC939", null, 90, "Controlled", [new UpdateRoutineSetWrite(SetType.Normal, 100, 10, null, null, null, new RepRange(8, 12))])]));

    public static CreateRoutineFolderRequest CreateRoutineFolderRequest() => new(new RoutineFolderWrite("Push Pull"));

    public static CreateExerciseTemplateRequest CreateExerciseTemplateRequest() => new(
        new CustomExerciseWrite(
            "Bench Press",
            CustomExerciseType.WeightReps,
            EquipmentCategory.Barbell,
            MuscleGroup.Chest,
            [MuscleGroup.Triceps, MuscleGroup.Shoulders]));

    public static CreateBodyMeasurementRequest CreateBodyMeasurementRequest() => new(
        new DateOnly(2024, 8, 14), 80.5m, 65m, 18.5m, 38m, 115m, 95m, 35m, 35.5m, 28m, 28.5m, 85m, 80m, 95m, 55m, 55.5m, 37m, 37.5m);

    public static UpdateBodyMeasurementRequest UpdateBodyMeasurementRequest() => new(
        80.5m, 65m, 18.5m, 38m, 115m, 95m, 35m, 35.5m, 28m, 28.5m, 85m, 80m, 95m, 55m, 55.5m, 37m, 37.5m);
}
