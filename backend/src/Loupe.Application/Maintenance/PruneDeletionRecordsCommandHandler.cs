using MediatR;

namespace Loupe.Application.Maintenance;

public sealed class PruneDeletionRecordsCommandHandler(IDeletionRetention retention) : IRequestHandler<PruneDeletionRecordsCommand, int>
{
    public Task<int> Handle(PruneDeletionRecordsCommand request, CancellationToken cancellationToken) => retention.PruneAsync(cancellationToken);
}
