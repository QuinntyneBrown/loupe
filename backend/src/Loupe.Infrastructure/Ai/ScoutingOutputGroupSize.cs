using Loupe.Domain.Scouting;

namespace Loupe.Infrastructure.Ai;

public sealed record ScoutingOutputGroupSize(bool CannotAssess, int? Minimum, int? Maximum, string Reason, EvidenceBasis Basis, int[] CitedImages);
