using System.Net;
using Loupe.Application.ReferenceImports;

namespace Loupe.Infrastructure.ReferenceImports;

public sealed class DnsResolver : IDnsResolver
{
    public async Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
        await Dns.GetHostAddressesAsync(host, cancellationToken);
}
