namespace Loupe.Domain.Boards;

public sealed class BoardReference
{
    public required string OwnerId { get; init; }
    public Guid BoardId { get; init; }
    public Guid ReferenceId { get; init; }
}
