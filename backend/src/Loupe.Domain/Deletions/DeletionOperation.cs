namespace Loupe.Domain.Deletions;

public sealed class DeletionOperation
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string OwnerId { get; init; }
    public required string ResourceType { get; init; }
    public required Guid ResourceId { get; init; }
    public required DateTimeOffset DeletedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
    public required string[] MediaKeys { get; set; }
}
