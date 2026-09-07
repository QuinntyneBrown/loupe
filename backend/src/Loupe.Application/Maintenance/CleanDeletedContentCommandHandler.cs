using MediatR;

namespace Loupe.Application.Maintenance;

public sealed class CleanDeletedContentCommandHandler(IDeletedContentCleaner cleaner) : IRequestHandler<CleanDeletedContentCommand, int>
{
    public Task<int> Handle(CleanDeletedContentCommand request, CancellationToken cancellationToken) => cleaner.CleanAsync(cancellationToken);
}
