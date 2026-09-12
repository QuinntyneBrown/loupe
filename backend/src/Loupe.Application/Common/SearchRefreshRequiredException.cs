namespace Loupe.Application.Common;

/// <summary>A continuation cursor was minted under another embedding model; the results must be refreshed from the top.</summary>
public sealed class SearchRefreshRequiredException() : Exception("The results changed while you were browsing. Refresh to continue.");
