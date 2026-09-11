using MediatR;
namespace Loupe.Application.Sessions;

public sealed record SignInCommand(string? Email, string? Password, string? PreviousToken) : IRequest<SessionLoginResult>;
