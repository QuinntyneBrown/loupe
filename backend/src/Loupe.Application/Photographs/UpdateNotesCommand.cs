using MediatR;

namespace Loupe.Application.Photographs;

public sealed record UpdateNotesCommand(Guid Id, long Revision, string? Notes) : IRequest<PhotographResult>;
