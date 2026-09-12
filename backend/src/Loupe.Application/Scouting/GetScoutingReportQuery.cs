using Loupe.Domain.Scouting;
using MediatR;

namespace Loupe.Application.Scouting;

public sealed record GetScoutingReportQuery(Guid LocationId) : IRequest<SavedScoutingReport?>;
