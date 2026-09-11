namespace Loupe.Application.ReferenceImports;

public interface IReferenceSourceReader
{
    Task<ReferenceSourceResult> ReadAsync(string source, CancellationToken cancellationToken);
}
