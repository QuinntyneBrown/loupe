namespace Loupe.Domain.References;

public sealed record ImportedReferenceSource(string RequestedUrl, string FetchedUrl, DateTimeOffset RetrievedAt, string? Title, string? Attribution);
