using MediatR;

namespace Loupe.Application.Sessions;

public sealed record CompleteSignInCommand(string Issuer, string Subject, string Name, string? PreviousToken) : IRequest<string>;
