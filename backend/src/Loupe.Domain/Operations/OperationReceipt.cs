namespace Loupe.Domain.Operations;

public sealed class OperationReceipt
{
    public required string OwnerId { get; init; }
    public required string OperationType { get; init; }
    public required string Key { get; init; }
    public required string PayloadHash { get; set; }
    public Guid ResourceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
