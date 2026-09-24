using MediatR;
namespace Loupe.Application.Users;

public sealed record EnsureUserCommand(string Email, string Name, string Password) : IRequest;
