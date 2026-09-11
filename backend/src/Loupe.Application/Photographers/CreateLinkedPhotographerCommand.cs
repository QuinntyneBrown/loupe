using Loupe.Application.References;
using MediatR;

namespace Loupe.Application.Photographers;

public sealed record CreateLinkedPhotographerCommand(Guid ReferenceId, long Revision, string? Name, string? PortfolioUrl, string? OperationKey) : IRequest<ReferenceResult>;
