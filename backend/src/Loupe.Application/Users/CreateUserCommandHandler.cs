using Loupe.Application.Common;
using Loupe.Domain.Users;
using MediatR;
namespace Loupe.Application.Users;

public sealed class CreateUserCommandHandler(IUserStore users, IPasswordService passwords) : IRequestHandler<CreateUserCommand, string>
{
    public async Task<string> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var email = CredentialValidation.Email(request.Email);
        CredentialValidation.Password(request.Password);
        var name = CredentialValidation.Name(request.Name);
        var user = new User { Id = Guid.NewGuid().ToString("N"), Email = email, NormalizedEmail = email.ToUpperInvariant(), Name = name, PasswordHash = passwords.Hash(request.Password) };
        if (!await users.TryCreateAsync(user, cancellationToken)) throw new RequestValidationException("email", "An account with this email already exists.");
        return user.Id;
    }
}
