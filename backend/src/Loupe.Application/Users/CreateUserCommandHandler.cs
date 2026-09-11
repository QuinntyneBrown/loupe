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
        var name = request.Name.Trim();
        if (name.EnumerateRunes().Count() is < 1 or > 200) throw new RequestValidationException("name", "Use a display name with 1 to 200 characters.");
        var user = new User { Id = Guid.NewGuid().ToString("N"), Email = email, NormalizedEmail = email.ToUpperInvariant(), Name = name, PasswordHash = passwords.Hash(request.Password) };
        await users.CreateAsync(user, cancellationToken);
        return user.Id;
    }
}
