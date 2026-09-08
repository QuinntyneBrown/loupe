using MediatR;

namespace Loupe.Application.References;

public sealed record GetReferenceQuery(Guid Id) : IRequest<ReferenceResult>;
