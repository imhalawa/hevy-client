using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hevy.Client.Models;

public sealed record PagedResult<T>(int Page, int PageCount, IReadOnlyList<T> Items);

public abstract record SetMetrics(
    decimal? WeightKg,
    decimal? Reps,
    decimal? DistanceMeters,
    decimal? DurationSeconds,
    decimal? Rpe,
    decimal? CustomMetric);

public sealed record RepRange(decimal? Start, decimal? End);

public enum EquipmentCategory
{
    [JsonStringEnumMemberName("none")]
    None,
    [JsonStringEnumMemberName("barbell")]
    Barbell,
    [JsonStringEnumMemberName("dumbbell")]
    Dumbbell,
    [JsonStringEnumMemberName("kettlebell")]
    Kettlebell,
    [JsonStringEnumMemberName("machine")]
    Machine,
    [JsonStringEnumMemberName("plate")]
    Plate,
    [JsonStringEnumMemberName("resistance_band")]
    ResistanceBand,
    [JsonStringEnumMemberName("suspension")]
    Suspension,
    [JsonStringEnumMemberName("other")]
    Other,
}

public enum MuscleGroup
{
    [JsonStringEnumMemberName("abdominals")]
    Abdominals,
    [JsonStringEnumMemberName("shoulders")]
    Shoulders,
    [JsonStringEnumMemberName("biceps")]
    Biceps,
    [JsonStringEnumMemberName("triceps")]
    Triceps,
    [JsonStringEnumMemberName("forearms")]
    Forearms,
    [JsonStringEnumMemberName("quadriceps")]
    Quadriceps,
    [JsonStringEnumMemberName("hamstrings")]
    Hamstrings,
    [JsonStringEnumMemberName("calves")]
    Calves,
    [JsonStringEnumMemberName("glutes")]
    Glutes,
    [JsonStringEnumMemberName("abductors")]
    Abductors,
    [JsonStringEnumMemberName("adductors")]
    Adductors,
    [JsonStringEnumMemberName("lats")]
    Lats,
    [JsonStringEnumMemberName("upper_back")]
    UpperBack,
    [JsonStringEnumMemberName("traps")]
    Traps,
    [JsonStringEnumMemberName("lower_back")]
    LowerBack,
    [JsonStringEnumMemberName("chest")]
    Chest,
    [JsonStringEnumMemberName("cardio")]
    Cardio,
    [JsonStringEnumMemberName("neck")]
    Neck,
    [JsonStringEnumMemberName("full_body")]
    FullBody,
    [JsonStringEnumMemberName("other")]
    Other,
}

public enum CustomExerciseType
{
    [JsonStringEnumMemberName("weight_reps")]
    WeightReps,
    [JsonStringEnumMemberName("reps_only")]
    RepsOnly,
    [JsonStringEnumMemberName("bodyweight_reps")]
    BodyweightReps,
    [JsonStringEnumMemberName("bodyweight_assisted_reps")]
    BodyweightAssistedReps,
    [JsonStringEnumMemberName("duration")]
    Duration,
    [JsonStringEnumMemberName("weight_duration")]
    WeightDuration,
    [JsonStringEnumMemberName("distance_duration")]
    DistanceDuration,
    [JsonStringEnumMemberName("short_distance_weight")]
    ShortDistanceWeight,
}

public enum SetType
{
    Warmup,
    Normal,
    Failure,
    Dropset,
}

public readonly record struct WorkoutRpe
{
    public WorkoutRpe(decimal value)
    {
        if (!IsValid(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "RPE must be one of Hevy's documented values.");
        }

        Value = value;
    }

    public decimal Value { get; }

    internal static bool IsValid(decimal value) => value is 6m or 7m or 7.5m or 8m or 8.5m or 9m or 9.5m or 10m;
}

public sealed class SetTypeJsonConverter : JsonConverter<SetType>
{
    public override SetType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.GetString() switch
        {
            "warmup" => SetType.Warmup,
            "normal" => SetType.Normal,
            "failure" => SetType.Failure,
            "dropset" => SetType.Dropset,
            _ => throw new JsonException("Set type must be one of Hevy's documented values."),
        };
    }

    public override void Write(Utf8JsonWriter writer, SetType value, JsonSerializerOptions options)
    {
        var wireValue = value switch
        {
            SetType.Warmup => "warmup",
            SetType.Normal => "normal",
            SetType.Failure => "failure",
            SetType.Dropset => "dropset",
            _ => throw new JsonException("Set type must be one of Hevy's documented values."),
        };

        writer.WriteStringValue(wireValue);
    }
}

public sealed class WorkoutRpeJsonConverter : JsonConverter<WorkoutRpe>
{
    public override WorkoutRpe Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new WorkoutRpe(reader.GetDecimal());
    }

    public override void Write(Utf8JsonWriter writer, WorkoutRpe value, JsonSerializerOptions options)
    {
        if (!WorkoutRpe.IsValid(value.Value))
        {
            throw new JsonException("RPE must be one of Hevy's documented values.");
        }

        writer.WriteNumberValue(value.Value);
    }
}
