using Loupe.Domain.Scouting;

namespace Loupe.Infrastructure.Ai;

public sealed record ScoutingOutputTechnique(CompositionTechnique Technique, string Explanation, EvidenceBasis Basis, int[] CitedImages);
