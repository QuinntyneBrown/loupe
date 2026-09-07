using MediatR;

namespace Loupe.Application.Maintenance;

public sealed record CleanAbandonedMediaCommand : IRequest<int>;
