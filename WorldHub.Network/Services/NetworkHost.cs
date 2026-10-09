using System.Collections.Concurrent;
using WorldHub.Logging;
using WorldHub.Network.Models;
using WorldHub.Network.Protocol;

namespace WorldHub.Network.Services;

public sealed class NetworkHost : IDisposable
{
    private static readonly TimeSpan ConnectionHandlerTimeout =
        TimeSpan.FromSeconds(10);

    private readonly NetworkService _networkService;
    private readonly int _port;
    private readonly Func<NetworkConnection, CancellationToken, Task> _connectionHandler;
    private readonly Action<string>? _log;
    private readonly Action<string>? _logError;
    private readonly ConcurrentDictionary<Task, byte> _activeHandlers = new();

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

    private void Log(string message)
    {
        if (_log is not null)
        {
            _log(message);
        }
        else
        {
            AppLog.Log(message);
        }
    }

    private void LogError(string message, Exception? exception = null)
    {
        if (_logError is not null)
        {
            _logError(message);
        }
        else
        {
            AppLog.Error(message, exception);
        }
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

        Log(
            $"WorldHub network listener starting on port {_port}.");
    }

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        NetworkListener? listener = null;

        try
        {
            listener = await _networkService.ListenAsync(_port, cancellationToken);
            Log($"WorldHub network listener started on port {_port}.");

            while (!cancellationToken.IsCancellationRequested)
            {
                NetworkConnection? connection = null;

                try
                {
                    Log($"Waiting for WorldHub connection on port {_port}...");

                    connection = await listener.AcceptAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception exception)
                {
                    LogError($"WorldHub network listener error: {exception}", exception);

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

                    continue;
                }

                try
                {
                    await NetworkHandshake.AcceptHelloAsync(
                        connection,
                        cancellationToken);
                }
                catch (Exception exception)
                {
                    LogError($"WorldHub handshake negotiation failed: {exception.Message}", exception);

                    await connection.DisposeAsync();
                    continue;
                }

                Log("WorldHub network handshake accepted successfully.");

                // Обработка соединения запускается в фоне с отслеживанием,
                // чтобы молчащий клиент не блокировал приём следующих.
                TrackHandler(connection, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Корректная отмена
        }
        catch (Exception exception)
        {
            LogError($"WorldHub network listener fatal error: {exception}", exception);
        }
        finally
        {
            if (listener is not null)
            {
                await listener.DisposeAsync();
            }

            Log("WorldHub network listener stopped.");
        }
    }

    private void TrackHandler(
        NetworkConnection connection,
        CancellationToken cancellationToken)
    {
        var task = HandleConnectionInBackgroundAsync(
            connection,
            cancellationToken);

        _activeHandlers.TryAdd(task, 0);

        _ = task.ContinueWith(
            completed => _activeHandlers.TryRemove(completed, out _),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task HandleConnectionInBackgroundAsync(
        NetworkConnection connection,
        CancellationToken cancellationToken)
    {
        await using (connection)
        {
            using var handlerCts =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);

            handlerCts.CancelAfter(ConnectionHandlerTimeout);

            try
            {
                await _connectionHandler(
                    connection,
                    handlerCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Таймаут handler'а или завершение приложения.
            }
            catch (EndOfStreamException)
            {
                Log(
                    "WorldHub network connection closed without transfer.");
            }
            catch (InvalidDataException exception)
            {
                LogError(
                    $"WorldHub transfer protocol error: {exception.Message}", exception);
            }
            catch (Exception exception)
            {
                LogError(
                    $"WorldHub incoming transfer failed: {exception}", exception);
            }
        }
    }

    public void Dispose()
    {
        var cancellationTokenSource = _cancellationTokenSource;

        _cancellationTokenSource = null;

        if (cancellationTokenSource is null)
        {
            return;
        }

        try
        {
            cancellationTokenSource.Cancel();
        }
        catch
        {
            // ignore
        }

        try
        {
            if (_listenerTask is not null)
            {
                _listenerTask.Wait(TimeSpan.FromSeconds(2));
            }

            var handlers = _activeHandlers.Keys.ToArray();
            if (handlers.Length > 0)
            {
                Task.WaitAll(handlers, TimeSpan.FromSeconds(3));
            }
        }
        catch
        {
            // ignore wait timeouts
        }
        finally
        {
            cancellationTokenSource.Dispose();
            _listenerTask = null;
            Log("WorldHub network service disposed.");
        }
    }
}