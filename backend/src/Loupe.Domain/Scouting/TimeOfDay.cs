using System.Text.Json.Serialization;

namespace Loupe.Domain.Scouting;

public enum TimeOfDay
{
    [JsonStringEnumMemberName("Dawn")] Dawn,
    [JsonStringEnumMemberName("Morning")] Morning,
    [JsonStringEnumMemberName("Midday")] Midday,
    [JsonStringEnumMemberName("Afternoon")] Afternoon,
    [JsonStringEnumMemberName("Golden hour")] GoldenHour,
    [JsonStringEnumMemberName("Blue hour")] BlueHour,
    [JsonStringEnumMemberName("Night")] Night,
}
