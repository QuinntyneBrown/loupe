using Loupe.Application.Operations;
using Loupe.Domain.Photographers;

namespace Loupe.Application.PhotographerDrafts;

public sealed record PhotographerDraftResult(Guid Id, string PortfolioUrl, string? Name, long Revision, DateTimeOffset ExpiresAt,
    Guid? CommittedPhotographerId, string? FailureCode, OperationResult? Import)
{
    public static PhotographerDraftResult From(PhotographerDraft draft) => new(draft.Id, draft.PortfolioUrl, draft.Name, draft.Revision,
        draft.ExpiresAt, draft.CommittedPhotographerId, draft.FailureCode ?? draft.ImportOperation?.FailureCode,
        draft.ImportOperation is null ? null : OperationResult.From(draft.ImportOperation));
}
