using Loupe.Domain.Sessions;
using MediatR;

namespace Loupe.Application.Sessions;

public sealed class AuthenticateSessionQueryHandler(ISessionStore store, IJwtService jwt, TimeProvider clock) : IRequestHandler<AuthenticateSessionQuery, ApplicationSession?>
{
    public async Task<ApplicationSession?> Handle(AuthenticateSessionQuery request, CancellationToken cancellationToken)
    {
        var id = SessionToken.Hash(request.Token);
        if (id is null) return null;
        var subject = await jwt.ValidateAsync(request.Token!);
        if (string.IsNullOrWhiteSpace(subject)) return null;
        var now = clock.GetUtcNow();
        var session = await store.ReadAndTouchAsync(id, now, now.AddHours(-12), now.AddMinutes(-30), cancellationToken);
        return session?.Subject == subject ? session : null;
    }
}
