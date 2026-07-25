using System.Text.Json;
using Hevy.Client.Models;
using Hevy.Client.Serialization;
using TestSupport;
using Xunit;

namespace Hevy.Client.Tests.Serialization;

public sealed class ResponseDeserializationTests
{
    // Break caught: removing forward-compatible unknown-property handling from workout parsing.
    [Fact]
    public void Workout_accepts_unknown_additive_fields()
    {
        var json = Fixture.Read("workout.json").Replace("\"title\":", "\"future_field\":42,\"title\":", StringComparison.Ordinal);

        var workout = JsonSerializer.Deserialize(json, HevyJsonContext.Default.Workout);

        Assert.Equal("workout-1", workout!.Id);
        Assert.Equal("Bench Press (Barbell)", workout.Exercises[0].Title);
        Assert.Equal(100, workout.Exercises[0].Sets[0].WeightKg);
    }

    // Break caught: mapping the routine's nested rep-range or snake_case folder identifier incorrectly.
    [Fact]
    public void Routine_deserializes_nested_set_metrics()
    {
        var routine = JsonSerializer.Deserialize(Fixture.Read("routine.json"), HevyJsonContext.Default.Routine);

        Assert.Equal("routine-1", routine!.Id);
        Assert.Equal(42, routine.FolderId);
        Assert.Equal(8, routine.Exercises[0].Sets[0].RepRange!.Start);
    }

    // Break caught: changing exercise-template JSON names or collection typing.
    [Fact]
    public void Exercise_template_deserializes_catalog_fields()
    {
        var template = JsonSerializer.Deserialize(Fixture.Read("exercise-template.json"), HevyJsonContext.Default.ExerciseTemplate);

        Assert.Equal("D04AC939", template!.Id);
        Assert.Equal(MuscleGroup.Chest, template.PrimaryMuscleGroup);
        Assert.Equal(MuscleGroup.Shoulders, template.SecondaryMuscleGroups[1]);
    }

    // Break caught: mapping a nullable history metric or set_type to the wrong wire field.
    [Fact]
    public void Exercise_history_deserializes_set_metrics()
    {
        var entry = JsonSerializer.Deserialize(Fixture.Read("exercise-history.json"), HevyJsonContext.Default.ExerciseHistoryEntry);

        Assert.Equal("workout-1", entry!.WorkoutId);
        Assert.Equal(10, entry.Reps);
        Assert.Equal("normal", entry.SetType);
    }

    // Break caught: parsing the date-only measurement key or numeric measurement fields incorrectly.
    [Fact]
    public void Body_measurement_deserializes_date_and_metrics()
    {
        var measurement = JsonSerializer.Deserialize(Fixture.Read("body-measurement.json"), HevyJsonContext.Default.BodyMeasurement);

        Assert.Equal(new DateOnly(2024, 8, 14), measurement!.Date);
        Assert.Equal(80.5m, measurement.WeightKg!.Value);
        Assert.Equal(37.5m, measurement.RightCalf!.Value);
    }

    // Break caught: failing to preserve the documented data envelope around user info.
    [Fact]
    public void User_info_deserializes_data_envelope()
    {
        var response = JsonSerializer.Deserialize(Fixture.Read("user-info.json"), HevyJsonContext.Default.UserInfoResponse);

        Assert.Equal("user-1", response!.Data.Id);
        Assert.Equal("Sanitized User", response.Data.Name);
    }

    // Break caught: mapping routine-folder index or timestamps to incorrect wire names.
    [Fact]
    public void Routine_folder_deserializes_identity_and_order()
    {
        var folder = JsonSerializer.Deserialize(Fixture.Read("routine-folder.json"), HevyJsonContext.Default.RoutineFolder);

        Assert.Equal(42, folder!.Id);
        Assert.Equal(1, folder.Index);
        Assert.Equal("Push Pull", folder.Title);
    }

    // Break caught: losing either side of the documented updated/deleted workout event union.
    [Fact]
    public void Workout_events_deserialize_updated_and_deleted_variants()
    {
        var events = JsonSerializer.Deserialize(Fixture.Read("workout-events.json"), HevyJsonContext.Default.WorkoutEventsPage);

        var updated = Assert.IsType<UpdatedWorkoutEvent>(events!.Events[0]);
        var deleted = Assert.IsType<DeletedWorkoutEvent>(events.Events[1]);
        Assert.Equal("workout-1", updated.Workout.Id);
        Assert.Equal("workout-2", deleted.Id);
    }
}
