using Loupe.Domain.References;
using MediatR;

namespace Loupe.Application.ReferenceAnalysis;

public sealed record GetReferenceSuggestionsQuery(Guid ReferenceId) : IRequest<SavedReferenceSuggestions?>;
