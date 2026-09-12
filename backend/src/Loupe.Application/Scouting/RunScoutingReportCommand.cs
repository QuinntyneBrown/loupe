using MediatR;

namespace Loupe.Application.Scouting;

public sealed record RunScoutingReportCommand : IRequest<bool>;
