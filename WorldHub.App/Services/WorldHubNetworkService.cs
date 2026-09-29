using WorldHub.Network.Models;
using WorldHub.Network.Services;

namespace WorldHub.App.Services;

public sealed class WorldHubNetworkService : IDisposable
{
    private const int DefaultPort = 27072;

    private readonly NetworkService _networkService;
    private readonly int _port;

    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _listenerTask;

    public WorldHubNetworkService(
        NetworkService networkService,
        int port = DefaultPort)
    {
        ArgumentNullException.ThrowIfNull(networkService);

        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(
                nameof(port));
        }

        _networkService = networkService;
        _port = port;
    }

    public int Port => _port;

    public void Start()
    {
        if (_listenerTask is not null)
        {
            return;
        }

        _cancellationTokenSource =
            new CancellationTokenSource();

        _listenerTask =
            ListenLoopAsync(
                _cancellationTokenSource.Token);

        DebugConsole.Log(
            $"WorldHub network listener starting on port {_port}.");
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

    private async Task ListenLoopAsync(
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                DebugConsole.Log(
                    $"Waiting for WorldHub connection on port {_port}...");

                await using var connection =
                    await _networkService.AcceptAsync(
                        _port,
                        cancellationToken);

                DebugConsole.Log(
                    "WorldHub network handshake accepted successfully.");
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                DebugConsole.Error(
                    $"WorldHub network listener error: {exception}");

                if (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(
                            TimeSpan.FromSeconds(1),
                            cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        DebugConsole.Log(
            "WorldHub network listener stopped.");
    }

    public void Dispose()
    {
        var cancellationTokenSource =
            _cancellationTokenSource;

        _cancellationTokenSource = null;

        if (cancellationTokenSource is null)
        {
            return;
        }

        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();

        _listenerTask = null;

        DebugConsole.Log(
            "WorldHub network service disposed.");
    }
}