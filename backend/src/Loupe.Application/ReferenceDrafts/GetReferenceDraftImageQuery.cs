using Loupe.Application.Images;
using MediatR;
namespace Loupe.Application.ReferenceDrafts;

public sealed record GetReferenceDraftImageQuery(Guid Id, bool Preview) : IRequest<ImageContent>;
