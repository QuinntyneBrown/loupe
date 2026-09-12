using System.Text.Json.Serialization;

namespace Loupe.Domain.Scouting;

public enum EvidenceBasis
{
    [JsonStringEnumMemberName("Visible")] Visible,
    [JsonStringEnumMemberName("Inferred")] Inferred,
}
