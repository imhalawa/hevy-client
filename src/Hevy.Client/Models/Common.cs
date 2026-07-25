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
