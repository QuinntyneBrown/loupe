using Loupe.Api.Photographers;
using Loupe.Application.Photographers;
using Loupe.Application.Deletions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loupe.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/photographers")]
public sealed class PhotographersController(ISender sender) : ControllerBase
{
    [HttpGet("{id:guid}/reference-candidates")]
    public Task<ReferenceCandidatePage> Candidates(Guid id, CancellationToken cancellationToken, [FromQuery] string? query = null, [FromQuery] int pageSize = 24, [FromQuery] string? cursor = null) =>
        sender.Send(new ListReferenceCandidatesQuery(id, query, pageSize, cursor), cancellationToken);

    [HttpDelete("{id:guid}")]
    public Task<DeletionResult> Delete(Guid id, [FromQuery] long revision, CancellationToken cancellationToken) =>
        sender.Send(new DeletePhotographerCommand(id, revision), cancellationToken);

    [HttpGet]
    public Task<PhotographerPage> List(CancellationToken cancellationToken, [FromQuery] int pageSize = 24, [FromQuery] string? cursor = null, [FromQuery] string? query = null) =>
        sender.Send(new ListPhotographersQuery(pageSize, cursor, query), cancellationToken);

    [HttpPost]
    public async Task<ActionResult<SavePhotographerResult>> Save(SavePhotographerRequest request, [FromHeader(Name = "Idempotency-Key")] string? operationKey, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SavePhotographerCommand(request.Name, request.PortfolioUrl, request.Summary, request.Notes, request.Tags, operationKey), cancellationToken);
        return result.AlreadySaved ? Ok(result) : Created($"/api/photographers/{result.Photographer.Id}", result);
    }
    [HttpGet("{id:guid}")]
    public Task<PhotographerResult> Get(Guid id, CancellationToken cancellationToken) => sender.Send(new GetPhotographerQuery(id), cancellationToken);

    [HttpGet("{id:guid}/references")]
    public Task<PhotographerReferencePage> References(Guid id, CancellationToken cancellationToken, [FromQuery] int pageSize = 24, [FromQuery] string? cursor = null) =>
        sender.Send(new ListPhotographerReferencesQuery(id, pageSize, cursor), cancellationToken);

    [HttpPut("{id:guid}")]
    public Task<PhotographerResult> Update(Guid id, UpdatePhotographerRequest request, CancellationToken cancellationToken) =>
        sender.Send(new UpdatePhotographerCommand(id, request.Revision, request.Name, request.PortfolioUrl, request.Summary, request.Notes, request.Tags), cancellationToken);
}
