namespace Loupe.Application.ReferenceImports;

public sealed class SourceFetchException(SourceFetchFailureKind kind) : Exception("The source could not be safely fetched.")
{
    public SourceFetchFailureKind Kind { get; } = kind;
}
