using Loupe.Application.Deletions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loupe.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/deletions")]
public sealed class DeletionsController(ISender sender) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public Task<DeletionResult> Get(Guid id, CancellationToken cancellationToken) => sender.Send(new GetDeletionQuery(id), cancellationToken);
}
