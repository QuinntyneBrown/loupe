using MediatR;

namespace Loupe.Application.Critiques;

public sealed record RunCritiqueCommand : IRequest<bool>;
