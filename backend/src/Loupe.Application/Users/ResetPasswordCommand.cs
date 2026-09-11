using MediatR;
namespace Loupe.Application.Users;

public sealed record ResetPasswordCommand(string Email, string Password) : IRequest;
