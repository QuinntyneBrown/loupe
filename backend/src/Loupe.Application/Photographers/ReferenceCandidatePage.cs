namespace Loupe.Application.Photographers;

public sealed record ReferenceCandidatePage(IReadOnlyList<ReferenceCandidate> Items, string? NextCursor, int TotalCount);
