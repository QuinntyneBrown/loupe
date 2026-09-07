namespace Loupe.Application.Security;

public interface ICurrentOwner
{
    string Subject { get; }
    string Name { get; }
}
