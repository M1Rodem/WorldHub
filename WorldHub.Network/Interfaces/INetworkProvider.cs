using WorldHub.Network.Models;

namespace WorldHub.Network.Interfaces;

public interface INetworkProvider
{
    Task<NetworkConnection> ConnectAsync(
        string host,
        int port,
        CancellationToken cancellationToken = default);

    Task<NetworkListener> ListenAsync(
        int port,
        CancellationToken cancellationToken = default);
}