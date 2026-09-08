using System.Net;
using System.Net.Sockets;
using Loupe.Application.ReferenceImports;

namespace Loupe.Infrastructure.ReferenceImports;

public sealed class SocketSourceConnector : ISourceConnector
{
    public async Task<Stream> ConnectAsync(IPAddress address, int port, CancellationToken cancellationToken)
    {
        var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(new IPEndPoint(address, port), cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch { socket.Dispose(); throw; }
    }
}
