using Loupe.Domain.Sessions;
using MediatR;

namespace Loupe.Application.Sessions;

public sealed record AuthenticateSessionQuery(string? Token) : IRequest<ApplicationSession?>;
