namespace Loupe.Application.Security;

public interface ICurrentOwner
{
    string Id { get; }
    string Subject { get; }
    string Name { get; }
}
