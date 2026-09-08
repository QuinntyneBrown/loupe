using System.Net;

namespace Loupe.Application.ReferenceImports;

public interface IDnsResolver
{
    Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken);
}
