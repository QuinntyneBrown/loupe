using Loupe.Domain.Scouting;

namespace Loupe.Infrastructure.Ai;

public sealed record ScoutingOutputSuitability(ShootType ShootType, SuitabilityRating Rating, string Reason, EvidenceBasis Basis, int[] CitedImages);
