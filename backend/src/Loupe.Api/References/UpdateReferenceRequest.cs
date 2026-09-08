namespace Loupe.Api.References;

public sealed record UpdateReferenceRequest(long Revision, string? Title, string? SourceUrl, string? Attribution, string? Notes);
