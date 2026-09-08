namespace Loupe.Api.References;

public sealed record SaveReferenceUrlRequest(string? SourceUrl, string? Title, string? Attribution, string? Notes);
