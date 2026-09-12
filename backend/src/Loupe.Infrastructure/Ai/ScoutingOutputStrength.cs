using Loupe.Domain.Scouting;

namespace Loupe.Infrastructure.Ai;

public sealed record ScoutingOutputStrength(string Strength, string Reason, EvidenceBasis Basis, int[] CitedImages);
