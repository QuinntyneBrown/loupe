namespace Loupe.Domain.Scouting;

public sealed record SuitabilityEntry(ShootType ShootType, SuitabilityRating Rating, string Reason, EvidenceBasis Basis, IReadOnlyList<Guid> CitedImageIds);
