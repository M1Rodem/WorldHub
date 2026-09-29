using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;

namespace WorldHub.App.Services;

public sealed class WorldHubTcpListener : IDisposable
{
    private const int DefaultPort = 27071;
    private const int MaxMessageSize = 1024 * 1024;

    private readonly WorldHubSessionService _sessionService;
    private readonly int _port;

    private TcpListener? _listener;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _listenTask;

    public WorldHubTcpListener(
        WorldHubSessionService sessionService,
        int port = DefaultPort)
    {
        ArgumentNullException.ThrowIfNull(sessionService);

        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(
                nameof(port));
        }

        _sessionService = sessionService;
        _port = port;
    }

    public int Port => _port;

    public void Start()
    {
        if (_listenTask is not null)
        {
            return;
        }

        _cancellationTokenSource =
            new CancellationTokenSource();

        _listener = new TcpListener(
            IPAddress.Loopback,
            _port);

        _listener.Start();

        DebugConsole.Log(
            $"WorldHub TCP listener started on 127.0.0.1:{_port}");

        _listenTask = ListenAsync(
            _cancellationTokenSource.Token);
    }

    private async Task ListenAsync(
        CancellationToken cancellationToken)
    {
        if (_listener is null)
        {
            return;
        }

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(
                    cancellationToken);

                _ = HandleClientAsync(
                    client,
                    cancellationToken);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            DebugConsole.Log(
                "WorldHub TCP listener stopped.");
        }
        catch (ObjectDisposedException)
        {
            DebugConsole.Log(
                "WorldHub TCP listener disposed.");
        }
        catch (Exception ex)
        {
            DebugConsole.Log(
                $"WorldHub TCP listener error: {ex}");
        }
    }

    private async Task HandleClientAsync(
        TcpClient client,
        CancellationToken cancellationToken)
    {
        using (client)

            try
            {
                await using var stream = client.GetStream();

                var message = await ReadMessageAsync(
                    stream,
                    cancellationToken);

                if (message is null)
                {
                    return;
                }

                DebugConsole.Log(
                    $"WorldHub TCP message received: {message}");

                if (string.Equals(
                        message,
                        "WORLDHUB_HELLO",
                        StringComparison.Ordinal))
                {
                    await WriteMessageAsync(
                        stream,
                        "WORLDHUB_READY",
                        cancellationToken);

                    return;
                }

                if (message.StartsWith(
                        "SESSION_ENDED|",
                        StringComparison.Ordinal))
                {
                    await _sessionService.HandleSessionEndedAsync(
                        message,
                        cancellationToken);

                    return;
                }

                DebugConsole.Log(
                    $"Unknown WorldHub TCP message: {message}");
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                DebugConsole.Log(
                    $"WorldHub TCP client error: {ex}");
            }
    }

    private static async Task<string?> ReadMessageAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        var lengthBuffer = new byte[4];

        if (!await ReadExactlyAsync(
                stream,
                lengthBuffer,
                cancellationToken))
        {
            return null;
        }

        var length =
            (lengthBuffer[0] << 24) |
            (lengthBuffer[1] << 16) |
            (lengthBuffer[2] << 8) |
            lengthBuffer[3];

        if (length <= 0 || length > MaxMessageSize)
        {
            throw new InvalidDataException(
                $"Invalid message length: {length}");
        }

        var payload = new byte[length];

        if (!await ReadExactlyAsync(
                stream,
                payload,
                cancellationToken))
        {
            return null;
        }

        return Encoding.UTF8.GetString(payload);
    }

    private static async Task WriteMessageAsync(
        NetworkStream stream,
        string message,
        CancellationToken cancellationToken)
    {
        var payload = Encoding.UTF8.GetBytes(message);

        if (payload.Length > MaxMessageSize)
        {
            throw new InvalidDataException(
                $"Message is too large: {payload.Length}");
        }

        var lengthBuffer = new byte[4];

        lengthBuffer[0] = (byte)(payload.Length >> 24);
        lengthBuffer[1] = (byte)(payload.Length >> 16);
        lengthBuffer[2] = (byte)(payload.Length >> 8);
        lengthBuffer[3] = (byte)payload.Length;

        await stream.WriteAsync(
            lengthBuffer,
            cancellationToken);

        await stream.WriteAsync(
            payload,
            cancellationToken);

        await stream.FlushAsync(
            cancellationToken);
    }

    private static async Task<bool> ReadExactlyAsync(
        NetworkStream stream,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        var offset = 0;

        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(
                buffer.AsMemory(offset),
                cancellationToken);

            if (read == 0)
            {
                return false;
            }

            offset += read;
        }

        return true;
    }

    public void Dispose()
    {
        try
        {
            _cancellationTokenSource?.Cancel();
        }
        catch
        {
        }

        try
        {
            _listener?.Stop();
        }
        catch
        {
        }

        _cancellationTokenSource?.Dispose();

        _listener = null;
        _cancellationTokenSource = null;
        _listenTask = null;
    }
}