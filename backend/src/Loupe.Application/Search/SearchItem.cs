namespace Loupe.Application.Search;

public sealed record SearchItem(Guid Id, string Type, string Title, DateTimeOffset CreatedAt, string? PreviewUrl, string? SourceUrl, string? Attribution, string? Description, int? Width, int? Height);
