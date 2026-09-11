namespace Loupe.Domain.References;

public sealed class ReferenceDraft
{
    public required Guid Id { get; init; }
    public required string OwnerId { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public required string Title { get; set; }
    public string? SourceUrl { get; set; }
    public string? SourceImportJson { get; set; }
    public string? Attribution { get; set; }
    public string? ImageKey { get; set; }
    public string? PreviewKey { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public long Revision { get; set; } = 1;
    public Guid? CommittedReferenceId { get; set; }
    public string? FailureCode { get; set; }
    public Guid? ImportOperationId { get; set; }
    public Loupe.Domain.Operations.BackgroundOperation? ImportOperation { get; set; }
}
