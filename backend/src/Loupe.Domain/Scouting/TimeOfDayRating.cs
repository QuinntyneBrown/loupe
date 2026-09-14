using System.Text.Json.Serialization;

namespace Loupe.Domain.Scouting;

public enum TimeOfDayRating
{
    [JsonStringEnumMemberName("Recommended")] Recommended,
    [JsonStringEnumMemberName("Avoid")] Avoid,
    [JsonStringEnumMemberName("Unknown")] Unknown,
}
