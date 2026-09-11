namespace Loupe.Domain.Photographers;

public sealed class PhotographerDraft
{
    public required Guid Id { get; init; }
    public required string OwnerId { get; init; }
    public required string PortfolioUrl { get; init; }
    public string? Name { get; set; }
    public string? SourceJson { get; set; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public long Revision { get; set; } = 1;
    public bool Canceled { get; set; }
    public Guid? CommittedPhotographerId { get; set; }
    public string? FailureCode { get; set; }
    public Guid? ImportOperationId { get; set; }
    public Loupe.Domain.Operations.BackgroundOperation? ImportOperation { get; set; }
}
