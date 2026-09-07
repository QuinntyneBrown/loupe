using MediatR;

namespace Loupe.Application.Photographs;

public sealed record UpdateBriefCommand(Guid Id, long Revision, CritiqueBriefInput Brief) : IRequest<PhotographResult>;
