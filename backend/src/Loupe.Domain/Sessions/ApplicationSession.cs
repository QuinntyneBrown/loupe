namespace Loupe.Domain.Sessions;

public sealed class ApplicationSession
{
    public required string Id { get; init; }
    public required string Issuer { get; init; }
    public required string Subject { get; init; }
    public required string Name { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset LastSeenAt { get; set; }
}
