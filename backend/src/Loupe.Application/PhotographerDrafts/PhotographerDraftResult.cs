using Loupe.Application.Operations;
using Loupe.Domain.Photographers;
using System.Text.Json;

namespace Loupe.Application.PhotographerDrafts;

public sealed record PhotographerDraftResult(Guid Id, string PortfolioUrl, string? Name, long Revision, DateTimeOffset ExpiresAt,
    Guid? CommittedPhotographerId, string? FailureCode, OperationResult? Import, string? Description, string[] Tags, CapturedPortfolioPage? Source)
{
    public static PhotographerDraftResult From(PhotographerDraft draft)
    {
        var source = draft.SourceJson is null ? null : JsonSerializer.Deserialize<CapturedPortfolioPage>(draft.SourceJson);
        return new(draft.Id, draft.PortfolioUrl, draft.Name, draft.Revision,
        draft.ExpiresAt, draft.CommittedPhotographerId, draft.FailureCode ?? draft.ImportOperation?.FailureCode,
        draft.ImportOperation is null ? null : OperationResult.From(draft.ImportOperation), source?.Description, source?.Tags ?? [], source);
    }
}
