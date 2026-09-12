using Loupe.Application.References;
using MediatR;

namespace Loupe.Application.ReferenceAnalysis;

public sealed record UndoReferenceSuggestionCommand(Guid Id, long Revision) : IRequest<ReferenceResult>;
