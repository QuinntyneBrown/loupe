namespace Loupe.Domain.Critiques;

public sealed record CritiqueObservation(bool Assessable, string Explanation, string? UncertaintyReason, EvidenceStatement[] Evidence);
