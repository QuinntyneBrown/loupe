using Loupe.Domain.Critiques;
using MediatR;

namespace Loupe.Application.Critiques;

public sealed record GetCritiqueQuery(Guid PhotographId) : IRequest<SavedCritique?>;
