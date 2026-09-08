using System.Net;

namespace Loupe.Application.ReferenceImports;

public interface ISourceConnector
{
    Task<Stream> ConnectAsync(IPAddress address, int port, CancellationToken cancellationToken);
}
