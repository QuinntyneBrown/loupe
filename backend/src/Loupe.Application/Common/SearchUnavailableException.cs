namespace Loupe.Application.Common;

/// <summary>Meaning search cannot run because the embedding service is unconfigured or failing; keyword search is unaffected.</summary>
public sealed class SearchUnavailableException() : Exception("Meaning search is not available right now.");
