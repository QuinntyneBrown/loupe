using System.Text.Json;
using System.Text.Json.Serialization;
using Loupe.Domain.Scouting;

namespace Loupe.Application.Scouting;

/// <summary>Serialization of the saved report: enum labels as text, so stored JSON reads with the shared vocabulary.</summary>
public static class ScoutingReportJson
{
    public static readonly JsonSerializerOptions Options = new() { Converters = { new JsonStringEnumConverter() } };

    public static string Serialize(SavedScoutingReport report) => JsonSerializer.Serialize(report, Options);

    public static SavedScoutingReport? Deserialize(string? json) => json is null ? null : JsonSerializer.Deserialize<SavedScoutingReport>(json, Options);
}
