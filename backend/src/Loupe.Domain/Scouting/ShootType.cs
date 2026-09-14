using System.Text.Json.Serialization;

namespace Loupe.Domain.Scouting;

public enum ShootType
{
    [JsonStringEnumMemberName("Portraits")] Portraits,
    [JsonStringEnumMemberName("Family portraits")] FamilyPortraits,
    [JsonStringEnumMemberName("Headshots")] Headshots,
    [JsonStringEnumMemberName("Engagement")] Engagement,
    [JsonStringEnumMemberName("Events")] Events,
}
