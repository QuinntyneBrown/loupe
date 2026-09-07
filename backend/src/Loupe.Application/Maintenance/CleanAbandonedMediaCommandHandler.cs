using MediatR;

namespace Loupe.Application.Maintenance;

public sealed class CleanAbandonedMediaCommandHandler(IAbandonedMediaCleaner cleaner) : IRequestHandler<CleanAbandonedMediaCommand, int>
{
    public Task<int> Handle(CleanAbandonedMediaCommand request, CancellationToken cancellationToken) => cleaner.CleanAsync(cancellationToken);
}
