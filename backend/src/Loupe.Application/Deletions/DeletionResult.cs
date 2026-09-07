using Loupe.Domain.Deletions;

namespace Loupe.Application.Deletions;

public sealed record DeletionResult(Guid Id, Guid ResourceId, DeletionStatus Status, DateTimeOffset DeletedAt, DateTimeOffset? CompletedAt)
{
    public static DeletionResult From(DeletionOperation operation) => new(operation.Id, operation.ResourceId,
        operation.CompletedAt is null ? DeletionStatus.Pending : DeletionStatus.Completed, operation.DeletedAt, operation.CompletedAt);
}
