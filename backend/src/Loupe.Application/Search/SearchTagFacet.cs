namespace Loupe.Application.Search;

public sealed record SearchTagFacet(string Name, int Count, string NormalizedName, string[] SelectedNames);
