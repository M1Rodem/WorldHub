using System.Net.Sockets;

namespace WorldHub.Network.Models;

public sealed class NetworkConnection : IAsyncDisposable
{
    public NetworkConnection(TcpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        Client = client;
        Stream = client.GetStream();
    }

    public TcpClient Client { get; }

    public NetworkStream Stream { get; }

    public ValueTask DisposeAsync()
    {
        Stream.Dispose();
        Client.Dispose();

        return ValueTask.CompletedTask;
    }
}