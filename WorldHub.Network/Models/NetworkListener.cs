using System.Net;
using System.Net.Sockets;

namespace WorldHub.Network.Models;

public sealed class NetworkListener : IAsyncDisposable
{
    private readonly TcpListener _listener;

    public NetworkListener(
        IPAddress address,
        int port)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port));
        }

        _listener = new TcpListener(address, port);
        _listener.Start();
    }

    public async Task<NetworkConnection> AcceptAsync(
        CancellationToken cancellationToken = default)
    {
        var client = await _listener.AcceptTcpClientAsync(
            cancellationToken);

        return new NetworkConnection(client);
    }

    public ValueTask DisposeAsync()
    {
        _listener.Stop();

        return ValueTask.CompletedTask;
    }
}