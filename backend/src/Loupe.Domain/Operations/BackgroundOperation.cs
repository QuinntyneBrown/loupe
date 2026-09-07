namespace Loupe.Domain.Operations;

public sealed class BackgroundOperation
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string OwnerId { get; init; }
    public required Guid ResourceId { get; init; }
    public required OperationType Type { get; init; }
    public required ExecutionMode Mode { get; init; }
    public required string Model { get; init; }
    public required string PromptVersion { get; init; }
    public required string? InputJson { get; set; }
    public OperationStatus Status { get; set; } = OperationStatus.Queued;
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string Message { get; set; } = "Waiting to start.";
}
