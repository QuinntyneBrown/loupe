using Loupe.Domain.Sessions;
using MediatR;

namespace Loupe.Application.Sessions;

public sealed class CompleteSignInCommandHandler(ISessionStore store, TimeProvider clock) : IRequestHandler<CompleteSignInCommand, string>
{
    public async Task<string> Handle(CompleteSignInCommand request, CancellationToken cancellationToken)
    {
        var token = SessionToken.Create();
        var now = clock.GetUtcNow();
        await store.CreateAsync(new ApplicationSession
        {
            Id = SessionToken.Hash(token)!,
            Issuer = request.Issuer,
            Subject = request.Subject,
            Name = request.Name,
            CreatedAt = now,
            LastSeenAt = now
        }, SessionToken.Hash(request.PreviousToken), cancellationToken);
        return token;
    }
}
