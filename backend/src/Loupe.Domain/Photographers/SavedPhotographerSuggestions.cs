using Loupe.Domain.Operations;

namespace Loupe.Domain.Photographers;

public sealed record SavedPhotographerSuggestions(Guid OperationId, long SourceRevision, DateTimeOffset CreatedAt,
    ExecutionMode Mode, string Model, string PromptVersion, CapturedPortfolioPage Source, string? Summary,
    string SummaryStatus, PhotographerSuggestedTag[] Tags, string? UnavailableReason);
