namespace Loupe.Domain.Scouting;

public sealed record ReportStrength(string Strength, string Reason, EvidenceBasis Basis, IReadOnlyList<Guid> CitedImageIds);
