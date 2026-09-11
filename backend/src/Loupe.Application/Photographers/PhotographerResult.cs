using Loupe.Domain.Photographers;

namespace Loupe.Application.Photographers;

public sealed record PhotographerResult(Guid Id, string Name, string PortfolioUrl, DateTimeOffset CreatedAt,
    string? Summary, string? SummaryProvenance, string? Notes, long Revision, PhotographerTagResult[] Tags)
{
    public static PhotographerResult From(Photographer photographer) => new(photographer.Id, photographer.Name, photographer.PortfolioUrl,
        photographer.CreatedAt, photographer.Summary, photographer.SummaryProvenance, photographer.Notes, photographer.Revision,
        photographer.Tags.OrderBy(tag => tag.Name, StringComparer.OrdinalIgnoreCase).Select(tag => new PhotographerTagResult(tag.Name, tag.Category, tag.Provenance)).ToArray());
}
