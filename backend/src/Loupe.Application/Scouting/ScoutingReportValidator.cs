using Loupe.Domain.Scouting;

namespace Loupe.Application.Scouting;

public static class ScoutingReportValidator
{
    private const int TextMaximum = 1000;

    public static bool IsValid(ScoutingReport? report, ScoutingInput input)
    {
        if (report is null || input.Images is null || input.Images.Count == 0) return false;
        var analysed = input.Images.Select(image => image.ImageId).ToHashSet();
        bool Cites(IReadOnlyList<Guid>? cited) => cited is { Count: > 0 } && cited.All(analysed.Contains);
        bool Text(string? value) => !string.IsNullOrWhiteSpace(value) && value.EnumerateRunes().Count() <= TextMaximum;

        if (report.Overview is not { Count: >= 1 and <= 3 }
            || report.Overview.Any(entry => entry is null || !Text(entry.Strength) || !Text(entry.Reason) || !Defined(entry.Basis) || !Cites(entry.CitedImageIds)))
            return false;

        var shootTypes = Enum.GetValues<ShootType>();
        if (report.Suitability is null || report.Suitability.Count != shootTypes.Length
            || report.Suitability.Any(entry => entry is null || !Defined(entry.ShootType) || !Defined(entry.Rating) || !Text(entry.Reason) || !Defined(entry.Basis) || !Cites(entry.CitedImageIds))
            || report.Suitability.Select(entry => entry.ShootType).Distinct().Count() != shootTypes.Length)
            return false;

        var periods = Enum.GetValues<TimeOfDay>();
        if (report.TimesOfDay is null || report.TimesOfDay.Count != periods.Length
            || report.TimesOfDay.Any(entry => entry is null || !Defined(entry.Period) || !Defined(entry.Rating) || !Text(entry.Reason) || !Defined(entry.Basis) || !Cites(entry.CitedImageIds))
            || report.TimesOfDay.Select(entry => entry.Period).Distinct().Count() != periods.Length)
            return false;
        // Recommended and Avoid rest on visible evidence; a supportable set names at least one Recommended period.
        if (report.TimesOfDay.Any(entry => entry.Rating != TimeOfDayRating.Unknown && entry.Basis != EvidenceBasis.Visible)) return false;
        if (report.TimesOfDay.Any(entry => entry.Rating != TimeOfDayRating.Unknown) && report.TimesOfDay.All(entry => entry.Rating != TimeOfDayRating.Recommended)) return false;

        if (report.Techniques is not { Count: >= 1 and <= 12 }
            || report.Techniques.Any(entry => entry is null || !Defined(entry.Technique) || !Text(entry.Explanation) || !Defined(entry.Basis) || !Cites(entry.CitedImageIds))
            || report.Techniques.Select(entry => entry.Technique).Distinct().Count() != report.Techniques.Count)
            return false;

        var group = report.GroupSize;
        if (group is null || !Text(group.Reason) || !Defined(group.Basis) || !Cites(group.CitedImageIds)) return false;
        if (group.CannotAssess ? group.Minimum is not null || group.Maximum is not null
            : group.Minimum is not >= 1 || group.Maximum is not <= 500 || group.Minimum > group.Maximum)
            return false;

        return report.Cautions is not null
            && report.Cautions.All(entry => entry is not null && Text(entry.Caution) && Defined(entry.Basis) && Cites(entry.CitedImageIds));
    }

    private static bool Defined<T>(T value) where T : struct, Enum => Enum.IsDefined(value);
}
