using MediatR;
namespace Loupe.Application.Users;

public sealed record CreateUserCommand(string Email, string Name, string Password) : IRequest<string>;
