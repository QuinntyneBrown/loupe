using Loupe.Application.References;

namespace Loupe.Application.Photographers;

public sealed record PhotographerSummary(Guid Id, string Name, string PortfolioUrl, DateTimeOffset CreatedAt, string? Summary,
    IReadOnlyList<PhotographerTagResult> Tags, int ReferenceCount, IReadOnlyList<ReferenceSummary> References);
