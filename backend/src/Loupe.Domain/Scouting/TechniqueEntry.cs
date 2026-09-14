namespace Loupe.Domain.Scouting;

public sealed record TechniqueEntry(CompositionTechnique Technique, string Explanation, EvidenceBasis Basis, IReadOnlyList<Guid> CitedImageIds);
