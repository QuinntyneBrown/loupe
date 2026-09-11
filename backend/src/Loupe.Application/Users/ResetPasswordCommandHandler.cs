using MediatR;
namespace Loupe.Application.Users;

public sealed class ResetPasswordCommandHandler(IUserStore users, IPasswordService passwords) : IRequestHandler<ResetPasswordCommand>
{
    public async Task Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var email = CredentialValidation.Email(request.Email);
        CredentialValidation.Password(request.Password);
        await users.ResetPasswordAsync(email.ToUpperInvariant(), passwords.Hash(request.Password), cancellationToken);
    }
}
