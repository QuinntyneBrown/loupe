using Loupe.Application.References;

namespace Loupe.Application.Photographers;

public sealed record PhotographerReferencePage(IReadOnlyList<ReferenceSummary> Items, string? NextCursor, int TotalCount);
