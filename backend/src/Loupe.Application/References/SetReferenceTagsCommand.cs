using MediatR;

namespace Loupe.Application.References;

public sealed record SetReferenceTagsCommand(Guid ReferenceId, long Revision, ReferenceTagInput[]? Tags) : IRequest<ReferenceResult>;
