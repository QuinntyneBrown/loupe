using Loupe.Application.References;
using MediatR;

namespace Loupe.Application.ReferenceAnalysis;

public sealed record ReviewReferenceSuggestionCommand(Guid Id, Guid OperationId, long Revision, string Target, string Decision,
    string? Name, string? Value, string? Category) : IRequest<ReferenceResult>;
