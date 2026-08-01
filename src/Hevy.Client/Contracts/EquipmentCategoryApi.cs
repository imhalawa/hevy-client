using System.Text.Json.Serialization;

namespace Hevy.Client.Contracts;

public enum EquipmentCategoryApi
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
  Other
}
