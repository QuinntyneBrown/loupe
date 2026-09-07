using MediatR;

namespace Loupe.Application.Sessions;

public sealed record RevokeSessionCommand(string? Token) : IRequest;
