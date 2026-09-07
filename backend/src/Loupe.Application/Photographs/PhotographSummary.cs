namespace Loupe.Application.Photographs;

public sealed record PhotographSummary(Guid Id, string Title, DateTimeOffset CreatedAt, int Width, int Height, string PreviewUrl);
