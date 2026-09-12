using Loupe.Application.References;
using MediatR;

namespace Loupe.Application.Photographers;

public sealed record SetReferencePhotographerCommand(Guid ReferenceId, long Revision, Guid? PhotographerId) : IRequest<ReferenceResult>;
