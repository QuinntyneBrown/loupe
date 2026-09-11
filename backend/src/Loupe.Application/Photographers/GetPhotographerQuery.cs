using MediatR;

namespace Loupe.Application.Photographers;

public sealed record GetPhotographerQuery(Guid Id) : IRequest<PhotographerResult>;
