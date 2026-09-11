using Loupe.Domain.References;

namespace Loupe.Application.References;

public sealed record ReferenceResult(Guid Id, string Title, DateTimeOffset CreatedAt, string? SourceUrl,
    string? Attribution, string? Notes, int? Width, int? Height, string? ImageUrl, string? PreviewUrl, long Revision, IReadOnlyList<Guid> BoardIds, IReadOnlyList<ReferenceTagResult> Tags,
    string? Description, string? DescriptionProvenance)
{
    public static ReferenceResult From(Reference reference) => new(reference.Id, reference.Title, reference.CreatedAt,
        reference.SourceUrl, reference.Attribution, reference.Notes, reference.Width, reference.Height,
        reference.ImageKey is null ? null : $"/api/references/{reference.Id}/image" + (reference.ImageRevision > 1 ? $"?v={reference.ImageRevision}" : ""),
        reference.PreviewKey is null ? null : $"/api/references/{reference.Id}/preview" + (reference.ImageRevision > 1 ? $"?v={reference.ImageRevision}" : ""), reference.Revision,
        reference.Boards.Select(item => item.BoardId).Order().ToArray(),
        reference.Tags.OrderBy(tag => tag.Name, StringComparer.OrdinalIgnoreCase).Select(tag => new ReferenceTagResult(tag.Name, tag.Category, tag.Provenance)).ToArray(), reference.Description, reference.DescriptionProvenance);
}
