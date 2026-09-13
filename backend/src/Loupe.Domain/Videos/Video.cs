namespace Loupe.Domain.Videos;

public sealed class Video
{
    public required Guid Id { get; init; }
    public required string OwnerId { get; init; }
    public required string Title { get; set; }
    public required string VideoId { get; set; }
    public required string Topic { get; set; }
    public required DateTimeOffset CreatedAt { get; init; }
    public string? Channel { get; set; }
    public string? Summary { get; set; }
    public string? Notes { get; set; }
    public long Revision { get; set; } = 1;
    public long? EmbeddedRevision { get; set; }
    public string? EmbeddingModel { get; set; }
    public ICollection<VideoTag> Tags { get; } = new List<VideoTag>();
}
