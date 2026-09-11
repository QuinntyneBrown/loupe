using Loupe.Application.Sessions;
using Loupe.Api.Authentication;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Loupe.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/session")]
public sealed class SessionsController(ISender sender) : ControllerBase
{
    [HttpPost("sign-out")]
    public IActionResult End() => SignOut("Cookies");
    [HttpGet("csrf")]
    [AllowAnonymous]
    public IActionResult Csrf() => NoContent();
    [HttpPost("sign-in")]
    [AllowAnonymous]
    public async Task<IActionResult> SignIn([FromBody] SignInRequest request, CancellationToken cancellationToken) =>
        new SessionCreatedResult(await sender.Send(new SignInCommand(request.Email, request.Password, Request.Cookies["__Host-loupe-session"]), cancellationToken));
    [HttpGet]
    public Task<SessionResult> Get(CancellationToken cancellationToken) => sender.Send(new GetSessionQuery(), cancellationToken);
}
