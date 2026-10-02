using WorldHub.Network.Models;

namespace WorldHub.Network.Services;

public sealed class NetworkHost : IDisposable
{
    private readonly NetworkService _networkService;
    private readonly int _port;
    private readonly Func<NetworkConnection, CancellationToken, Task> _connectionHandler;
    private readonly Action<string>? _log;
    private readonly Action<string>? _logError;

    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _listenerTask;

    public NetworkHost(
        NetworkService networkService,
        int port,
        Func<NetworkConnection, CancellationToken, Task> connectionHandler,
        Action<string>? log = null,
        Action<string>? logError = null)
    {
        ArgumentNullException.ThrowIfNull(networkService);
        ArgumentNullException.ThrowIfNull(connectionHandler);

        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port));
        }

        _networkService = networkService;
        _port = port;
        _connectionHandler = connectionHandler;
        _log = log;
        _logError = logError;
    }

    public int Port => _port;

    public void Start()
    {
        if (_listenerTask is not null)
        {
            return;
        }

        _cancellationTokenSource = new CancellationTokenSource();

        _listenerTask = ListenLoopAsync(_cancellationTokenSource.Token);

        _log?.Invoke(
            $"WorldHub network listener starting on port {_port}.");
    }

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _log?.Invoke(
                    $"Waiting for WorldHub connection on port {_port}...");

                await using var connection =
                    await _networkService.AcceptAsync(
                        _port,
                        cancellationToken);

                _log?.Invoke(
                    "WorldHub network handshake accepted successfully.");

                try
                {
                    await _connectionHandler(connection, cancellationToken);
                }
                catch (EndOfStreamException)
                {
                    _log?.Invoke(
                        "WorldHub network connection closed without transfer.");
                }
                catch (InvalidDataException exception)
                {
                    _logError?.Invoke(
                        $"WorldHub transfer protocol error: {exception.Message}");
                }
                catch (Exception exception)
                {
                    _logError?.Invoke(
                        $"WorldHub incoming transfer failed: {exception}");
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                _logError?.Invoke(
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

        _log?.Invoke("WorldHub network listener stopped.");
    }

    public void Dispose()
    {
        var cancellationTokenSource = _cancellationTokenSource;

        _cancellationTokenSource = null;

        if (cancellationTokenSource is null)
        {
            return;
        }

        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();

        _listenerTask = null;

        _log?.Invoke("WorldHub network service disposed.");
    }
}