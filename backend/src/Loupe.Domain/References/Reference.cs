namespace Loupe.Domain.References;

public sealed class Reference
{
    public required Guid Id { get; init; }
    public required string OwnerId { get; init; }
    public required string Title { get; set; }
    public required DateTimeOffset CreatedAt { get; init; }
    public string? SourceUrl { get; set; }
    public string? SourceImportJson { get; set; }
    public string? Attribution { get; set; }
    public Guid? PhotographerId { get; set; }
    public Loupe.Domain.Photographers.Photographer? Photographer { get; set; }
    public string? Notes { get; set; }
    public string? Description { get; set; }
    public string? DescriptionProvenance { get; set; }
    public string? ImageKey { get; set; }
    public string? PreviewKey { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public Guid? CurrentImportOperationId { get; set; }
    public Guid? CurrentAnalysisOperationId { get; set; }
    public string? SuggestionsJson { get; set; }
    public string? SuggestionUndoJson { get; set; }
    public long Revision { get; set; } = 1;
    public long ImageRevision { get; set; } = 1;
    public ICollection<ReferenceTag> Tags { get; } = new List<ReferenceTag>();
    public ICollection<Loupe.Domain.Boards.BoardReference> Boards { get; } = new List<Loupe.Domain.Boards.BoardReference>();
}
