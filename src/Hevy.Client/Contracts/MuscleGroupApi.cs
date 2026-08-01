using System.Text.Json.Serialization;

namespace Hevy.Client.Contracts;

public enum MuscleGroupApi
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
  Other
}
