using MediatR;

namespace Loupe.Application.Photographers;

public sealed record UpdatePhotographerCommand(Guid Id, long Revision, string? Name, string? PortfolioUrl, string? Summary, string? Notes, PhotographerTagInput[]? Tags) : IRequest<PhotographerResult>;
