using Loupe.Domain.Scouting;

namespace Loupe.Infrastructure.Ai;

public sealed record ScoutingOutputCaution(string Caution, EvidenceBasis Basis, int[] CitedImages);
