namespace Loupe.Application.ReferenceImports;

public sealed class SourceImportException(string code) : Exception("The source could not be imported.")
{
    public string Code { get; } = code;
}
