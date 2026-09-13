using Loupe.Application.Security;
using Loupe.Domain.Videos;
using MediatR;

namespace Loupe.Application.Videos;

public sealed class SaveVideoCommandHandler(ICurrentOwner owner, IVideoStore videos, TimeProvider clock, IVideoEmbeddingConfiguration embeddings) : IRequestHandler<SaveVideoCommand, VideoResult>
{
    public async Task<VideoResult> Handle(SaveVideoCommand request, CancellationToken cancellationToken)
    {
        var metadata = VideoMetadataValidator.Normalize(request.Title, request.Url, request.Topic, request.Channel, request.Summary, request.Notes, request.Tags);
        var video = new Video
        {
            Id = Guid.NewGuid(),
            OwnerId = owner.Id,
            Title = metadata.Title,
            VideoId = metadata.VideoId,
            Topic = metadata.Topic,
            Channel = metadata.Channel,
            Summary = metadata.Summary,
            Notes = metadata.Notes,
            CreatedAt = clock.GetUtcNow()
        };
        foreach (var tag in metadata.Tags) video.Tags.Add(new VideoTag
        {
            VideoId = video.Id,
            OwnerId = owner.Id,
            Name = tag.Name!,
            NormalizedName = tag.Name!.ToUpperInvariant(),
            Category = tag.Category,
            Provenance = "manual"
        });
        return VideoResult.From(await videos.SaveAsync(video, cancellationToken), embeddings.CurrentModel);
    }
}
