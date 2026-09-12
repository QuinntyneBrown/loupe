namespace Loupe.Api.ReferenceDrafts;

public sealed record SaveReferenceDraftRequest(long Revision, string? Title, string? SourceUrl, string? Attribution, string? Notes, Guid[]? BoardIds);
