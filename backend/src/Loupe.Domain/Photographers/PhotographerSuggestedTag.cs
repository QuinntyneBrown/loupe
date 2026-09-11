namespace Loupe.Domain.Photographers;

public sealed record PhotographerSuggestedTag(string Name, string Category, string State = "pending");
