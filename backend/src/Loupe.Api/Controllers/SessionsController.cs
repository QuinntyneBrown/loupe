using Loupe.Application.Sessions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace Loupe.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/session")]
public sealed class SessionsController(ISender sender) : ControllerBase
{
    [HttpGet("sign-in")]
    [AllowAnonymous]
    public async Task<IActionResult> SignIn([FromQuery] string? returnUrl, CancellationToken cancellationToken) =>
        Challenge(new AuthenticationProperties { RedirectUri = await sender.Send(new BeginSignInQuery(returnUrl), cancellationToken) }, "oidc");

    [HttpGet]
    public Task<SessionResult> Get(CancellationToken cancellationToken) =>
        sender.Send(new GetSessionQuery(), cancellationToken);
}
