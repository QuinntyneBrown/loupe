using Loupe.Domain.Photographers;
using MediatR;

namespace Loupe.Application.PhotographerSummaries;

public sealed record GetPhotographerSuggestionsQuery(Guid PhotographerId) : IRequest<SavedPhotographerSuggestions?>;
