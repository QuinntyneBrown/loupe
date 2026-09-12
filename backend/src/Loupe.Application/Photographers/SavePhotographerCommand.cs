using MediatR;

namespace Loupe.Application.Photographers;

public sealed record SavePhotographerCommand(string? Name, string? PortfolioUrl, string? Summary, string? Notes, PhotographerTagInput[]? Tags, string? OperationKey) : IRequest<SavePhotographerResult>;
