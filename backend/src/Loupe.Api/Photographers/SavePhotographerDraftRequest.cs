using Loupe.Application.Photographers;

namespace Loupe.Api.Photographers;

public sealed record SavePhotographerDraftRequest(long Revision, string? Name, string? PortfolioUrl, string? Summary, string? Notes, PhotographerTagInput[]? Tags);
