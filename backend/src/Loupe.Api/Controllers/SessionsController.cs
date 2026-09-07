using Loupe.Application.Sessions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loupe.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/session")]
public sealed class SessionsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public Task<SessionResult> Get(CancellationToken cancellationToken) =>
        sender.Send(new GetSessionQuery(), cancellationToken);
}
