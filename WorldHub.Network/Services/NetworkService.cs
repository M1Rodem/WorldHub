using WorldHub.Network.Interfaces;
using WorldHub.Network.Models;
using WorldHub.Network.Protocol;

namespace WorldHub.Network.Services;

public sealed class NetworkService
{
    private readonly INetworkProvider _networkProvider;

    public NetworkService(INetworkProvider networkProvider)
    {
        ArgumentNullException.ThrowIfNull(networkProvider);

        _networkProvider = networkProvider;
    }

    public async Task<NetworkConnection> ConnectAsync(
        string host,
        int port,
        CancellationToken cancellationToken = default)
    {
        var connection = await _networkProvider.ConnectAsync(
            host,
            port,
            cancellationToken);

        try
        {
            await NetworkHandshake.SendHelloAsync(
                connection,
                cancellationToken);

            await NetworkHandshake.WaitForServerHelloAsync(
                connection,
                cancellationToken);

            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public Task<NetworkListener> ListenAsync(
        int port,
        CancellationToken cancellationToken = default)
    {
        return _networkProvider.ListenAsync(
            port,
            cancellationToken);
    }

    public async Task<NetworkConnection> AcceptAsync(
        int port,
        CancellationToken cancellationToken = default)
    {
        await using var listener = await _networkProvider.ListenAsync(
            port,
            cancellationToken);

        var connection = await listener.AcceptAsync(
            cancellationToken);

        try
        {
            await NetworkHandshake.AcceptHelloAsync(
                connection,
                cancellationToken);

            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}