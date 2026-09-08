using Loupe.Domain.Operations;

namespace Loupe.Application.Operations;

public sealed record OperationResult(Guid Id, Guid ResourceId, OperationType Type, OperationStatus Status,
    ExecutionMode Mode, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? CompletedAt,
    DateTimeOffset? NextAttemptAt, DateTimeOffset? RetryAvailableAt, string? FailureCode, string Message)
{
    public static OperationResult From(BackgroundOperation operation) => new(operation.Id, operation.ResourceId,
        operation.Type, operation.Status, operation.Mode, operation.CreatedAt, operation.UpdatedAt, operation.CompletedAt,
        operation.NextAttemptAt, operation.RetryAvailableAt, operation.FailureCode, operation.Message);
}
