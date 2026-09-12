using System.Text.Json.Serialization;

namespace Loupe.Domain.Scouting;

public enum SuitabilityRating
{
    [JsonStringEnumMemberName("Well suited")] WellSuited,
    [JsonStringEnumMemberName("Workable")] Workable,
    [JsonStringEnumMemberName("Not recommended")] NotRecommended,
    [JsonStringEnumMemberName("Cannot assess")] CannotAssess,
}
