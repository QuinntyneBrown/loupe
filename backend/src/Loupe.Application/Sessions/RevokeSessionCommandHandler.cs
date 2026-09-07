using MediatR;

namespace Loupe.Application.Sessions;

public sealed class RevokeSessionCommandHandler(ISessionStore store) : IRequestHandler<RevokeSessionCommand>
{
    public async Task Handle(RevokeSessionCommand request, CancellationToken cancellationToken)
    {
        var id = SessionToken.Hash(request.Token);
        if (id is not null) await store.RemoveAsync(id, cancellationToken);
    }
}
