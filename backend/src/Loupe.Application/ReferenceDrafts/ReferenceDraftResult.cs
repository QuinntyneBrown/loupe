using Loupe.Domain.References;
using Loupe.Application.Operations;

namespace Loupe.Application.ReferenceDrafts;

public sealed record ReferenceDraftResult(Guid Id, string Title, string? SourceUrl, string? Attribution,
    int? Width, int? Height, string? ImageUrl, string? PreviewUrl, long Revision, DateTimeOffset ExpiresAt, Guid? CommittedReferenceId, string? FailureCode, OperationResult? Import)
{
    public static ReferenceDraftResult From(ReferenceDraft draft) => new(draft.Id, draft.Title, draft.SourceUrl, draft.Attribution,
        draft.Width, draft.Height, draft.ImageKey is null ? null : $"/api/reference-drafts/{draft.Id}/image",
        draft.PreviewKey is null ? null : $"/api/reference-drafts/{draft.Id}/preview", draft.Revision, draft.ExpiresAt, draft.CommittedReferenceId,
        draft.FailureCode ?? draft.ImportOperation?.FailureCode, draft.ImportOperation is null ? null : OperationResult.From(draft.ImportOperation));
}
