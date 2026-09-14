namespace Loupe.Domain.Scouting;

public sealed record GroupSizeEntry(bool CannotAssess, int? Minimum, int? Maximum, string Reason, EvidenceBasis Basis, IReadOnlyList<Guid> CitedImageIds);
