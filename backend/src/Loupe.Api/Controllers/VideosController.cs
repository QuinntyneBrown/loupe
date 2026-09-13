using Loupe.Api.Videos;
using Loupe.Application.Videos;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loupe.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/videos")]
public sealed class VideosController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<VideoResult>> Save(SaveVideoRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SaveVideoCommand(request.Title, request.Url, request.Topic, request.Channel, request.Summary, request.Notes, request.Tags), cancellationToken);
        return Created($"/api/videos/{result.Id}", result);
    }

    [HttpGet]
    public Task<VideoPage> List(CancellationToken cancellationToken, [FromQuery] string? query = null, [FromQuery] string? topic = null,
        [FromQuery] string[]? tags = null, [FromQuery] int pageSize = 24, [FromQuery] string? cursor = null, [FromQuery] string mode = "keyword") =>
        sender.Send(new ListVideosQuery(query, topic, tags, pageSize, cursor, mode), cancellationToken);

    [HttpGet("tags")]
    public Task<IReadOnlyList<VideoTagCount>> Tags(CancellationToken cancellationToken) => sender.Send(new ListVideoTagsQuery(), cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<VideoResult> Get(Guid id, CancellationToken cancellationToken) => sender.Send(new GetVideoQuery(id), cancellationToken);

    [HttpPut("{id:guid}")]
    public Task<VideoResult> Update(Guid id, UpdateVideoRequest request, CancellationToken cancellationToken) =>
        sender.Send(new UpdateVideoCommand(id, request.Revision, request.Title, request.Url, request.Topic, request.Channel, request.Summary, request.Notes, request.Tags), cancellationToken);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] long revision, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteVideoCommand(id, revision), cancellationToken);
        return NoContent();
    }
}
