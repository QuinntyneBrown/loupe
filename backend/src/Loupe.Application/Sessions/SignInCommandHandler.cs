using Loupe.Application.Users;
using Loupe.Domain.Sessions;
using MediatR;
namespace Loupe.Application.Sessions;

public sealed class SignInCommandHandler(IUserStore users, IPasswordService passwords, IJwtService jwt, ISessionStore sessions, TimeProvider clock)
    : IRequestHandler<SignInCommand, SessionLoginResult>
{
    public async Task<SessionLoginResult> Handle(SignInCommand request, CancellationToken cancellationToken)
    {
        var email = CredentialValidation.Email(request.Email);
        CredentialValidation.Password(request.Password);
        var user = await users.FindAsync(email.ToUpperInvariant(), cancellationToken);
        if (!passwords.Verify(user?.PasswordHash, request.Password!) || user is null) throw new InvalidCredentialsException();
        var now = clock.GetUtcNow();
        var session = new ApplicationSession
        {
            Id = "",
            Issuer = jwt.Issuer,
            Subject = user.Id,
            Name = user.Name,
            CreatedAt = now,
            LastSeenAt = now,
            UserVersion = user.PasswordVersion
        };
        var token = jwt.Create(session);
        session.Id = SessionToken.Hash(token)!;
        await sessions.CreateAsync(session, SessionToken.Hash(request.PreviousToken), cancellationToken);
        return new SessionLoginResult(token, new SessionResult(user.Id, user.Name));
    }
}
