namespace Loupe.Domain.Photographers;

public sealed class Photographer
{
    public required Guid Id { get; init; }
    public required string OwnerId { get; init; }
    public required string Name { get; set; }
    public required string PortfolioUrl { get; set; }
    public required DateTimeOffset CreatedAt { get; init; }
    public string? Summary { get; set; }
    public string? SummaryProvenance { get; set; }
    public string? Notes { get; set; }
    public long Revision { get; set; } = 1;
    public long SourceRevision { get; set; } = 1;
    public long? CapturedSourceRevision { get; set; }
    public string? SourceJson { get; set; }
    public string? SourceFailureCode { get; set; }
    public ICollection<PhotographerTag> Tags { get; } = new List<PhotographerTag>();
}
