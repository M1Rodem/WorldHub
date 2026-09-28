using System.Net;
using System.Net.Sockets;
using WorldHub.Network.Interfaces;
using WorldHub.Network.Models;

namespace WorldHub.Network.Providers;

public sealed class TcpNetworkProvider : INetworkProvider
{
    public async Task<NetworkConnection> ConnectAsync(
        string host,
        int port,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException(
                "Host cannot be empty.",
                nameof(host));
        }

        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(
                nameof(port));
        }

        var client = new TcpClient();

        try
        {
            await client.ConnectAsync(
                host,
                port,
                cancellationToken);

            return new NetworkConnection(client);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public Task<NetworkListener> ListenAsync(
        int port,
        CancellationToken cancellationToken = default)
    {
        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(
                nameof(port));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var listener = new NetworkListener(
            IPAddress.Any,
            port);

        return Task.FromResult(listener);
    }
}