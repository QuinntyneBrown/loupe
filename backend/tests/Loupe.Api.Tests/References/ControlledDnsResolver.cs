using System.Net;
using Loupe.Application.ReferenceImports;

namespace Loupe.Api.Tests.References;

/// <summary>Maps fixture hostnames to controlled addresses, so acceptance tests can exercise
/// forbidden-destination, redirect-to-forbidden, IPv4-mapped-IPv6, and DNS-rebinding fixtures
/// without touching real DNS or the network. See L2-040.</summary>
public sealed class ControlledDnsResolver : IDnsResolver
{
    private readonly Dictionary<string, IPAddress[]> map = new(StringComparer.OrdinalIgnoreCase);

    public ControlledDnsResolver Map(string host, params IPAddress[] addresses)
    {
        map[host] = addresses;
        return this;
    }

    public Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<IPAddress>>(map.TryGetValue(host, out var addresses) ? addresses : []);
}
