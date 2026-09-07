using Loupe.Application.Images;
using MediatR;

namespace Loupe.Application.Photographs;

public sealed record GetPhotographImageQuery(Guid Id, bool Preview) : IRequest<ImageContent>;
