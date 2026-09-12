using MediatR;

namespace Loupe.Application.PhotographerDrafts;

public sealed record GetPhotographerDraftQuery(Guid Id) : IRequest<PhotographerDraftResult>;
