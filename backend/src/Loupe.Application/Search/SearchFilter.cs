namespace Loupe.Application.Search;

public sealed record SearchFilter(string[] Tokens, string Type, string[] Tags, Guid[] BoardIds);
