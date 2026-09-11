using MediatR;

namespace Loupe.Application.PhotographerDrafts;

public sealed record ImportPhotographerDraftCommand(string? PortfolioUrl, string? Name, string? OperationKey) : IRequest<PhotographerDraftResult>;
