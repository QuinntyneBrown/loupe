namespace Loupe.Domain.Critiques;

public sealed record Improvement(string Observation, string Effect, string Action, EvidenceStatement[] Evidence);
