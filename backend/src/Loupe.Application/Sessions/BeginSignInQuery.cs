using MediatR;

namespace Loupe.Application.Sessions;

public sealed record BeginSignInQuery(string? ReturnUrl) : IRequest<string>;
