using WorldHub.App.Services.Diagnostics;
using WorldHub.App.Services.Application;
using WorldHub.Network.Services;
using WorldHub.Sync.Transfer;

namespace WorldHub.App.Services.Network;

public sealed class WorldHubNetworkService : IDisposable
{
    private const int DefaultPort = 27072;

    private readonly NetworkService _networkService;
    private readonly NetworkHost _networkHost;
    private readonly int _port;

    public WorldHubNetworkService(
        NetworkService networkService,
        WorldTransferOrchestrator transferService,
        int port = DefaultPort)
    {
        ArgumentNullException.ThrowIfNull(networkService);
        ArgumentNullException.ThrowIfNull(transferService);

        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port));
        }

        _networkService = networkService;
        _port = port;

        _networkHost = new NetworkHost(
            networkService,
            port,
            (connection, cancellationToken) =>
                transferService.HandleIncomingAsync(connection, cancellationToken),
            DebugConsole.Log,
            DebugConsole.Error);
    }

    public int Port => _port;

    public void Start()
    {
        _networkHost.Start();
    }

    public async Task<bool> ConnectAsync(
        string host,
        int port,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        DebugConsole.Log(
            $"WorldHub network connection requested: {host}:{port}");

        try
        {
            await using var connection =
                await _networkService.ConnectAsync(
                    host,
                    port,
                    cancellationToken);

            DebugConsole.Log(
                $"WorldHub network handshake succeeded: {host}:{port}");

            return true;
        }
        catch (OperationCanceledException)
        {
            DebugConsole.Log(
                $"WorldHub network connection cancelled: {host}:{port}");

            return false;
        }
        catch (Exception exception)
        {
            DebugConsole.Error(
                $"WorldHub network connection failed: " +
                $"{host}:{port}: {exception}");

            return false;
        }
    }

    public void Dispose()
    {
        _networkHost.Dispose();
    }
}