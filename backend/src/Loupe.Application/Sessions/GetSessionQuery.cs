using MediatR;

namespace Loupe.Application.Sessions;

public sealed record GetSessionQuery : IRequest<SessionResult>;
