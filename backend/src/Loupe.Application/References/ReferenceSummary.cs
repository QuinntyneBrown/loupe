namespace Loupe.Application.References;

public sealed record ReferenceSummary(Guid Id, string Title, DateTimeOffset CreatedAt, int? Width, int? Height,
    string? PreviewUrl, string? SourceUrl, string? Attribution);
