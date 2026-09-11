namespace Loupe.Domain.Photographs;

public sealed class Photograph
{
    public required Guid Id { get; init; }
    public required string OwnerId { get; init; }
    public required string Title { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required string ImageKey { get; init; }
    public required string PreviewKey { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public CaptureMetadata Exif { get; init; } = new();
    public CritiqueBrief Brief { get; set; } = new();
    public long Revision { get; set; } = 1;
    public string? Notes { get; set; }
    public string? ArchivedDemoCritiqueJson { get; set; }
    public string? CritiqueJson { get; set; }
    public Guid? CurrentCritiqueOperationId { get; set; }
}
