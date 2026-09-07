using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.Sessions;

public sealed class GetSessionQueryHandler(ICurrentOwner owner) : IRequestHandler<GetSessionQuery, SessionResult>
{
    public Task<SessionResult> Handle(GetSessionQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(new SessionResult(owner.Subject, owner.Name));
}
