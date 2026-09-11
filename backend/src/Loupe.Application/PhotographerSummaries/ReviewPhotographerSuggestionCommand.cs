using Loupe.Application.Photographers;
using MediatR;

namespace Loupe.Application.PhotographerSummaries;

public sealed record ReviewPhotographerSuggestionCommand(Guid Id, Guid OperationId, long Revision, string Target, string Decision,
    string? Name, string? Value, string? Category) : IRequest<PhotographerResult>;
