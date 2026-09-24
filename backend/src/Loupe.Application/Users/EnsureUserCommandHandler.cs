using Loupe.Domain.Users;
using MediatR;
namespace Loupe.Application.Users;

public sealed class EnsureUserCommandHandler(IUserStore users, IPasswordService passwords) : IRequestHandler<EnsureUserCommand>
{
    public async Task Handle(EnsureUserCommand request, CancellationToken cancellationToken)
    {
        var email = CredentialValidation.Email(request.Email);
        CredentialValidation.Password(request.Password);
        var name = CredentialValidation.Name(request.Name);
        var normalizedEmail = email.ToUpperInvariant();
        if (await users.FindAsync(normalizedEmail, cancellationToken) is not null) return;
        var user = new User { Id = Guid.NewGuid().ToString("N"), Email = email, NormalizedEmail = normalizedEmail, Name = name, PasswordHash = passwords.Hash(request.Password) };
        await users.TryCreateAsync(user, cancellationToken);
    }
}
