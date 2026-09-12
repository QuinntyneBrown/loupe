using MediatR;

namespace Loupe.Application.PhotographerDrafts;

public sealed record CancelPhotographerDraftCommand(Guid Id) : IRequest;
