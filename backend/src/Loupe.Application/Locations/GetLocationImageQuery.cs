using Loupe.Application.Images;
using MediatR;

namespace Loupe.Application.Locations;

public sealed record GetLocationImageQuery(Guid Id, Guid ImageId, bool Preview) : IRequest<ImageContent>;
