namespace Loupe.Domain.Scouting;

public sealed record ScoutingReport(IReadOnlyList<ReportStrength> Overview, IReadOnlyList<SuitabilityEntry> Suitability,
    IReadOnlyList<TimeOfDayEntry> TimesOfDay, IReadOnlyList<TechniqueEntry> Techniques, GroupSizeEntry GroupSize, IReadOnlyList<CautionEntry> Cautions);
