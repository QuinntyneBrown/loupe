namespace Loupe.Domain.Critiques;

public sealed record EvidenceStatement(EvidenceKind Kind, string Statement, string? ExifField = null, string? ExifValue = null, ImageRegion? Region = null);
