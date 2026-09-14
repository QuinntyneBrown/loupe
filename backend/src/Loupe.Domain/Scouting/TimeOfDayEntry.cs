namespace Loupe.Domain.Scouting;

public sealed record TimeOfDayEntry(TimeOfDay Period, TimeOfDayRating Rating, string Reason, EvidenceBasis Basis, IReadOnlyList<Guid> CitedImageIds);
