using Loupe.Api.ReferenceImports;
using Loupe.Application.ReferenceImports;
using Loupe.Application.Operations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Loupe.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/references/{id:guid}/imports")]
public sealed class ReferenceImportsController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<OperationResult>> Create(Guid id, RequestReferenceImportRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? operationKey, CancellationToken cancellationToken)
    {
        var operation = await sender.Send(new RequestReferenceImportCommand(id, request.Revision, operationKey), cancellationToken);
        return Accepted($"/api/operations/{operation.Id}", operation);
    }
}
