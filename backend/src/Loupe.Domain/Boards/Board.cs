namespace Loupe.Domain.Boards;

public sealed class Board
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string OwnerId { get; init; }
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public long Revision { get; set; } = 1;
    public ICollection<BoardReference> References { get; } = new List<BoardReference>();
}
