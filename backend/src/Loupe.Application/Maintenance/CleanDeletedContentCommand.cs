using MediatR;

namespace Loupe.Application.Maintenance;

public sealed record CleanDeletedContentCommand : IRequest<int>;
