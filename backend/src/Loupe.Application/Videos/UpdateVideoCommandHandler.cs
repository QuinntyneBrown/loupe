using Loupe.Application.Common;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.Videos;

public sealed class UpdateVideoCommandHandler(ICurrentOwner owner, IVideoStore videos, IVideoEmbeddingConfiguration embeddings) : IRequestHandler<UpdateVideoCommand, VideoResult>
{
    public async Task<VideoResult> Handle(UpdateVideoCommand request, CancellationToken cancellationToken)
    {
        if (request.Revision < 1) throw new RequestValidationException("revision", "Supply the video revision you opened.");
        var metadata = VideoMetadataValidator.Normalize(request.Title, request.Url, request.Topic, request.Channel, request.Summary, request.Notes, request.Tags);
        return VideoResult.From(await videos.UpdateAsync(owner.Id, request.Id, request.Revision, metadata, cancellationToken), embeddings.CurrentModel);
    }
}
