using Loupe.Application.Photographers;
using MediatR;

namespace Loupe.Application.PhotographerDrafts;

public sealed record SavePhotographerDraftCommand(Guid Id, long Revision, string? Name, string? PortfolioUrl, string? Summary, string? Notes,
    PhotographerTagInput[]? Tags, string? OperationKey) : IRequest<SavePhotographerResult>;
