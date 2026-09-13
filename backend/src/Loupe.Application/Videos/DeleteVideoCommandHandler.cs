using Loupe.Application.Common;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.Videos;

public sealed class DeleteVideoCommandHandler(ICurrentOwner owner, IVideoStore videos) : IRequestHandler<DeleteVideoCommand>
{
    public Task Handle(DeleteVideoCommand request, CancellationToken cancellationToken)
    {
        if (request.Revision < 1) throw new RequestValidationException("revision", "Supply the video revision you opened.");
        return videos.DeleteAsync(owner.Id, request.Id, request.Revision, cancellationToken);
    }
}
