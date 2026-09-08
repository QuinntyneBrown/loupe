using Loupe.Application.Images;
using MediatR;

namespace Loupe.Application.References;

public sealed record GetReferenceImageQuery(Guid Id, bool Preview) : IRequest<ImageContent>;
