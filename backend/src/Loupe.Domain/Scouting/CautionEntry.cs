namespace Loupe.Domain.Scouting;

public sealed record CautionEntry(string Caution, EvidenceBasis Basis, IReadOnlyList<Guid> CitedImageIds);
