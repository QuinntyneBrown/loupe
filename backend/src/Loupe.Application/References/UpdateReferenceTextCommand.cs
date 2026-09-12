using MediatR;

namespace Loupe.Application.References;

public sealed record UpdateReferenceTextCommand(Guid Id, long Revision, ReferenceTextField Field, string? Text) : IRequest<ReferenceResult>;
