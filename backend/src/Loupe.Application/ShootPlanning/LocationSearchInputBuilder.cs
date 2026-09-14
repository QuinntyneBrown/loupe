using System.Text;
using Loupe.Application.Scouting;
using Loupe.Domain.Locations;
using Loupe.Domain.Scouting;

namespace Loupe.Application.ShootPlanning;

/// <summary>The text a location's search vector is built from: descriptive fields and the current report, never a street address line or postal code.</summary>
public static class LocationSearchInputBuilder
{
    public static string From(Location location)
    {
        var document = new StringBuilder();
        document.AppendLine(location.Name);
        var place = new[] { location.Locality, location.Region, location.Country }.Where(part => !string.IsNullOrWhiteSpace(part)).ToArray();
        if (place.Length > 0) document.AppendLine(string.Join(", ", place));
        if (location.Setting is { } setting) document.AppendLine($"Setting: {setting}");
        var tags = location.Tags.OrderBy(tag => tag.Name, StringComparer.OrdinalIgnoreCase).Select(tag => tag.Name).ToArray();
        if (tags.Length > 0) document.AppendLine("Tags: " + string.Join(", ", tags));
        if (!string.IsNullOrWhiteSpace(location.ScoutingBrief)) document.AppendLine("Brief: " + location.ScoutingBrief);
        if (!string.IsNullOrWhiteSpace(location.Notes)) document.AppendLine("Notes: " + location.Notes);
        if (ScoutingReportJson.Deserialize(location.ScoutingReportJson)?.Report is { } report) Append(document, report);
        return document.ToString().TrimEnd();
    }

    private static void Append(StringBuilder document, ScoutingReport report)
    {
        document.AppendLine("Scouting report:");
        foreach (var strength in report.Overview) document.AppendLine($"{strength.Strength}. {strength.Reason}");
        foreach (var entry in report.Suitability) document.AppendLine($"{ScoutingVocabulary.Label(entry.ShootType)}: {ScoutingVocabulary.Label(entry.Rating)}. {entry.Reason}");
        foreach (var entry in report.TimesOfDay) document.AppendLine($"{ScoutingVocabulary.Label(entry.Period)}: {ScoutingVocabulary.Label(entry.Rating)}. {entry.Reason}");
        foreach (var technique in report.Techniques) document.AppendLine($"{ScoutingVocabulary.Label(technique.Technique)}: {technique.Explanation}");
        document.AppendLine(report.GroupSize.CannotAssess ? $"Group size: cannot assess. {report.GroupSize.Reason}"
            : $"Group size: {report.GroupSize.Minimum} to {report.GroupSize.Maximum} people. {report.GroupSize.Reason}");
        foreach (var caution in report.Cautions) document.AppendLine($"Caution: {caution.Caution}");
    }
}
