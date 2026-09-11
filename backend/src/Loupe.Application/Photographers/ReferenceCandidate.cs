namespace Loupe.Application.Photographers;

public sealed record ReferenceCandidate(Guid Id, string Title, DateTimeOffset CreatedAt, string? PreviewUrl, long Revision, ReferencePhotographerResult? Photographer);
