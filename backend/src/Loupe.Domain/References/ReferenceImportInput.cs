namespace Loupe.Domain.References;

public sealed record ReferenceImportInput(string SourceUrl, string NormalizedSource, string? ImageKey, long ReferenceRevision);
