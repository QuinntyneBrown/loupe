namespace Loupe.Domain.Users;

public sealed class User
{
    public required string Id { get; init; }
    public required string Email { get; init; }
    public required string NormalizedEmail { get; init; }
    public required string Name { get; init; }
    public required string PasswordHash { get; set; }
    public string PasswordVersion { get; set; } = Guid.NewGuid().ToString("N");
}
