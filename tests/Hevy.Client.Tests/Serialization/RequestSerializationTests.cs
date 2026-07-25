using System.Text.Json;
using Hevy.Client.Serialization;
using TestSupport;
using Xunit;

namespace Hevy.Client.Tests.Serialization;

public sealed class RequestSerializationTests
{
    // Break caught: serializing a create-workout payload in camelCase or including server timestamps.
    [Fact]
    public void Create_workout_serializes_only_writable_snake_case_fields()
    {
        var json = JsonSerializer.Serialize(FixtureFactory.CreateWorkoutRequest(), HevyJsonContext.Default.CreateWorkoutRequest);

        Assert.Contains("\"start_time\"", json, StringComparison.Ordinal);
        Assert.Contains("\"superset_id\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("updated_at", json, StringComparison.Ordinal);
        Assert.DoesNotContain("created_at", json, StringComparison.Ordinal);
    }

    // Break caught: using a distinct update-workout wrapper that changes the documented workout envelope.
    [Fact]
    public void Update_workout_preserves_workout_envelope()
    {
        var json = JsonSerializer.Serialize(FixtureFactory.UpdateWorkoutRequest(), HevyJsonContext.Default.UpdateWorkoutRequest);

        Assert.Contains("\"workout\":{", json, StringComparison.Ordinal);
        Assert.Contains("\"end_time\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"id\"", json, StringComparison.Ordinal);
    }

    // Break caught: emitting a create-routine folder identifier or set rep range under C# property names.
    [Fact]
    public void Create_routine_serializes_documented_fields()
    {
        var json = JsonSerializer.Serialize(FixtureFactory.CreateRoutineRequest(), HevyJsonContext.Default.CreateRoutineRequest);

        Assert.Contains("\"routine\":{", json, StringComparison.Ordinal);
        Assert.Contains("\"folder_id\":null", json, StringComparison.Ordinal);
        Assert.Contains("\"rep_range\":{", json, StringComparison.Ordinal);
        Assert.DoesNotContain("updated_at", json, StringComparison.Ordinal);
    }

    // Break caught: adding unsupported folder_id to the update-routine contract.
    [Fact]
    public void Update_routine_omits_create_only_folder_field()
    {
        var json = JsonSerializer.Serialize(FixtureFactory.UpdateRoutineRequest(), HevyJsonContext.Default.UpdateRoutineRequest);

        Assert.Contains("\"routine\":{", json, StringComparison.Ordinal);
        Assert.Contains("\"rest_seconds\":90", json, StringComparison.Ordinal);
        Assert.DoesNotContain("folder_id", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"id\"", json, StringComparison.Ordinal);
    }

    // Break caught: flattening the routine-folder create payload or camel-casing its wire name.
    [Fact]
    public void Create_routine_folder_serializes_routine_folder_envelope()
    {
        var json = JsonSerializer.Serialize(FixtureFactory.CreateRoutineFolderRequest(), HevyJsonContext.Default.CreateRoutineFolderRequest);

        Assert.Contains("\"routine_folder\":{\"title\":\"Push Pull\"}", json, StringComparison.Ordinal);
        Assert.DoesNotContain("updated_at", json, StringComparison.Ordinal);
    }

    // Break caught: serializing custom-exercise enum values or nested wrapper names incorrectly.
    [Fact]
    public void Create_exercise_template_serializes_enum_wire_values()
    {
        var json = JsonSerializer.Serialize(FixtureFactory.CreateExerciseTemplateRequest(), HevyJsonContext.Default.CreateExerciseTemplateRequest);

        Assert.Contains("\"exercise_type\":\"weight_reps\"", json, StringComparison.Ordinal);
        Assert.Contains("\"equipment_category\":\"barbell\"", json, StringComparison.Ordinal);
        Assert.Contains("\"other_muscles\":[\"triceps\",\"shoulders\"]", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"id\"", json, StringComparison.Ordinal);
    }

    // Break caught: serializing a create measurement without its required date or with a server identifier.
    [Fact]
    public void Create_body_measurement_serializes_date_and_metrics()
    {
        var json = JsonSerializer.Serialize(FixtureFactory.CreateBodyMeasurementRequest(), HevyJsonContext.Default.CreateBodyMeasurementRequest);

        Assert.Contains("\"date\":\"2024-08-14\"", json, StringComparison.Ordinal);
        Assert.Contains("\"weight_kg\":80.5", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"id\"", json, StringComparison.Ordinal);
    }

    // Break caught: accidentally including the path-owned measurement date in an update payload.
    [Fact]
    public void Update_body_measurement_omits_path_owned_date()
    {
        var json = JsonSerializer.Serialize(FixtureFactory.UpdateBodyMeasurementRequest(), HevyJsonContext.Default.UpdateBodyMeasurementRequest);

        Assert.Contains("\"weight_kg\":80.5", json, StringComparison.Ordinal);
        Assert.Contains("\"right_calf\":37.5", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"date\"", json, StringComparison.Ordinal);
    }
}
