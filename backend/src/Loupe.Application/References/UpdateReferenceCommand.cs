using MediatR;

namespace Loupe.Application.References;

public sealed record UpdateReferenceCommand(Guid Id, long Revision, string? Title, string? SourceUrl, string? Attribution, string? Notes) : IRequest<ReferenceResult>;
