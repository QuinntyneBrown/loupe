using MediatR;

namespace Loupe.Application.Photographs;

public sealed record GetPhotographQuery(Guid Id) : IRequest<PhotographResult>;
