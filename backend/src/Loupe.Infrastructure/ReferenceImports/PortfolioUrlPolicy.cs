using System.Net;
using Loupe.Application.Photographers;

namespace Loupe.Infrastructure.ReferenceImports;

public sealed class PortfolioUrlPolicy : IPortfolioUrlPolicy
{
    public bool IsAllowed(Uri source)
    {
        var host = source.IdnHost.Trim('[', ']', '.');
        if (IPAddress.TryParse(host, out var address)) return PublicAddressPolicy.IsPublic(address);
        return host.Contains('.') && !host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            && !host.EndsWith(".local", StringComparison.OrdinalIgnoreCase);
    }
}
