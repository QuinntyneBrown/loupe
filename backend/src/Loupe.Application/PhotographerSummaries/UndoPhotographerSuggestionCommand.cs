using Loupe.Application.Photographers;
using MediatR;

namespace Loupe.Application.PhotographerSummaries;

public sealed record UndoPhotographerSuggestionCommand(Guid Id, long Revision) : IRequest<PhotographerResult>;
