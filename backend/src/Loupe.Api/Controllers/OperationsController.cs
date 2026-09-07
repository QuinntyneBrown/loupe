using Loupe.Application.Operations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loupe.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/operations")]
public sealed class OperationsController(ISender sender) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public Task<OperationResult> Get(Guid id, CancellationToken cancellationToken) => sender.Send(new GetOperationQuery(id), cancellationToken);
}
