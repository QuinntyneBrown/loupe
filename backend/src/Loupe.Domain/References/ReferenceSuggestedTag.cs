namespace Loupe.Domain.References;

public sealed record ReferenceSuggestedTag(string Name, string Category, string State = "pending");
