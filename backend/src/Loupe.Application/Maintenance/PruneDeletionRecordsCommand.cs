using MediatR;

namespace Loupe.Application.Maintenance;

public sealed record PruneDeletionRecordsCommand : IRequest<int>;
