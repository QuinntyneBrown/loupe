using Loupe.Domain.Photographers;
using System.Text.Json;

namespace Loupe.Application.Photographers;

public sealed record PhotographerResult(Guid Id, string Name, string PortfolioUrl, DateTimeOffset CreatedAt,
    string? Summary, string? SummaryProvenance, string? Notes, long Revision, PhotographerTagResult[] Tags,
    long SourceRevision, CapturedPortfolioPage? Source, bool SourceIsCurrent, string? SourceFailureCode)
{
    public static PhotographerResult From(Photographer photographer) => new(photographer.Id, photographer.Name, photographer.PortfolioUrl,
        photographer.CreatedAt, photographer.Summary, photographer.SummaryProvenance, photographer.Notes, photographer.Revision,
        photographer.Tags.OrderBy(tag => tag.Name, StringComparer.OrdinalIgnoreCase).Select(tag => new PhotographerTagResult(tag.Name, tag.Category, tag.Provenance)).ToArray(),
        photographer.SourceRevision, photographer.SourceJson is null ? null : JsonSerializer.Deserialize<CapturedPortfolioPage>(photographer.SourceJson),
        photographer.SourceJson is not null && photographer.CapturedSourceRevision == photographer.SourceRevision, photographer.SourceFailureCode);
}
