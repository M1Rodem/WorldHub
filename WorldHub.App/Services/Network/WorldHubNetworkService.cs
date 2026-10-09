using System.IO;
using System.Net.Sockets;
using WorldHub.App.Services.Diagnostics;
using WorldHub.Network.Interfaces;
using WorldHub.Network.Models;
using WorldHub.Network.Protocol;
using WorldHub.Network.Services;

namespace WorldHub.App.Services.Network;

public sealed class WorldHubNetworkService : IDisposable
{
    private const int DefaultPort = 27072;

    private static readonly TimeSpan PeerCheckTimeout =
        TimeSpan.FromSeconds(5);

    private readonly NetworkService _networkService;
    private readonly INetworkProvider _networkProvider;
    private readonly NetworkHost _networkHost;
    private readonly string _deviceId;
    private readonly Func<string> _userNameProvider;
    private readonly Func<string> _versionProvider;
    private readonly Func<string> _googleDriveStatusProvider;
    private readonly Func<CancellationToken, Task<string?>> _googleEmailProvider;
    private readonly Func<string, CancellationToken, Task<ServerInfo>> _serverInfoProvider;
    private readonly CancellationTokenSource _disposeCts = new();

    private bool _disposed;

    public WorldHubNetworkService(
        NetworkService networkService,
        INetworkProvider networkProvider,
        string deviceId,
        Func<string> userNameProvider,
        Func<string> versionProvider,
        Func<string> googleDriveStatusProvider,
        Func<CancellationToken, Task<string?>> googleEmailProvider,
        Func<string, CancellationToken, Task<ServerInfo>> serverInfoProvider,
        int port = DefaultPort)
    {
        ArgumentNullException.ThrowIfNull(networkService);
        ArgumentNullException.ThrowIfNull(networkProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        ArgumentNullException.ThrowIfNull(userNameProvider);
        ArgumentNullException.ThrowIfNull(versionProvider);
        ArgumentNullException.ThrowIfNull(googleDriveStatusProvider);
        ArgumentNullException.ThrowIfNull(googleEmailProvider);
        ArgumentNullException.ThrowIfNull(serverInfoProvider);

        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port));
        }

        _networkService = networkService;
        _networkProvider = networkProvider;
        _deviceId = deviceId;
        _userNameProvider = userNameProvider;
        _versionProvider = versionProvider;
        _googleDriveStatusProvider = googleDriveStatusProvider;
        _googleEmailProvider = googleEmailProvider;
        _serverInfoProvider = serverInfoProvider;
        Port = port;

        _networkHost = new NetworkHost(
            networkService,
            port,
            HandleConnectionAsync,
            DebugConsole.Log,
            DebugConsole.Error);
    }

    public int Port { get; }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _networkHost.Start();
    }

    public async Task<PeerCheckResult> CheckPeerAsync(
        string host,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        using var linkedCts =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _disposeCts.Token);

        using var timeoutCts =
            CancellationTokenSource.CreateLinkedTokenSource(
                linkedCts.Token);

        timeoutCts.CancelAfter(PeerCheckTimeout);

        var token = timeoutCts.Token;

        NetworkConnection? connection = null;

        try
        {
            connection = await _networkProvider.ConnectAsync(
                host,
                Port,
                token);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            DebugConsole.Log(
                $"WorldHub TCP timeout for {host}.");

            return PeerCheckResult.TcpUnavailable();
        }
        catch (Exception exception)
        {
            DebugConsole.Log(
                $"WorldHub TCP unavailable for {host}: {exception.Message}");

            return PeerCheckResult.TcpUnavailable();
        }

        await using (connection)
        {
            try
            {
                await NetworkHandshake.SendHelloAsync(
                    connection,
                    token);

                await NetworkHandshake.WaitForServerHelloAsync(
                    connection,
                    token);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                DebugConsole.Log(
                    $"WorldHub handshake timeout for {host}.");

                return PeerCheckResult.HandshakeFailed();
            }
            catch (Exception exception)
            {
                DebugConsole.Log(
                    $"WorldHub handshake failed for {host}: {exception.Message}");

                return PeerCheckResult.HandshakeFailed();
            }

            try
            {
                await WorldTransferProtocol.SendMessageAsync(
                    connection,
                    PeerProtocol.Ping,
                    token);

                var pong = await WorldTransferProtocol.ReceiveMessageAsync(
                    connection,
                    token);

                if (pong != PeerProtocol.Pong)
                {
                    throw new InvalidDataException(
                        $"Expected PONG, received '{pong}'.");
                }

                await WorldTransferProtocol.SendMessageAsync(
                    connection,
                    PeerProtocol.WorldHub,
                    token);

                var response = await WorldTransferProtocol.ReceiveMessageAsync(
                    connection,
                    token);

                if (response != PeerProtocol.WorldHubOk)
                {
                    throw new InvalidDataException(
                        $"Expected WORLDHUB_OK, received '{response}'.");
                }

                var profile =
                    await WorldTransferProtocol.ReceiveJsonAsync<PeerInfo>(
                        connection,
                        token);

                DebugConsole.Log(
                    $"WorldHub peer verified: {host}, {profile.PcName}");

                return PeerCheckResult.ProfileReceived(profile);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                DebugConsole.Log(
                    $"WorldHub protocol timeout for {host}.");

                return PeerCheckResult.ProtocolFailed();
            }
            catch (Exception exception)
            {
                DebugConsole.Log(
                    $"WorldHub protocol failed for {host}: {exception.Message}");

                return PeerCheckResult.ProtocolFailed();
            }
        }
    }

    /// <summary>
    /// Запрашивает у удалённого WorldHub информацию о его сервере,
    /// в котором записан наш DeviceId.
    /// </summary>
    public async Task<ServerInfo?> RequestServerInfoAsync(
        string host,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        using var linkedCts =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _disposeCts.Token);

        using var timeoutCts =
            CancellationTokenSource.CreateLinkedTokenSource(
                linkedCts.Token);

        timeoutCts.CancelAfter(PeerCheckTimeout);

        var token = timeoutCts.Token;

        NetworkConnection? connection = null;

        try
        {
            connection = await _networkProvider.ConnectAsync(
                host,
                Port,
                token);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }

        await using (connection)
        {
            try
            {
                await NetworkHandshake.SendHelloAsync(connection, token);
                await NetworkHandshake.WaitForServerHelloAsync(connection, token);

                await WorldTransferProtocol.SendMessageAsync(
                    connection,
                    ServerInfoProtocol.ServerInfoRequest,
                    token);

                // Передаём наш DeviceId — чтобы A знал, какой его сервер нам нужен.
                await WorldTransferProtocol.SendMessageAsync(
                    connection,
                    _deviceId,
                    token);

                var response = await WorldTransferProtocol.ReceiveMessageAsync(
                    connection,
                    token);

                if (response != ServerInfoProtocol.ServerInfoResponse)
                {
                    return null;
                }

                return await WorldTransferProtocol.ReceiveJsonAsync<ServerInfo>(
                    connection,
                    token);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                return null;
            }
        }
    }

    private async Task HandleConnectionAsync(
        NetworkConnection connection,
        CancellationToken cancellationToken)
    {
        var command = await WorldTransferProtocol.ReceiveMessageAsync(
            connection,
            cancellationToken);

        // Ветка WORLDHUB_SERVER_INFO — без PING/PONG.
        if (command == ServerInfoProtocol.ServerInfoRequest)
        {
            var remoteDeviceId = await WorldTransferProtocol.ReceiveMessageAsync(
                connection,
                cancellationToken);

            ServerInfo info;

            try
            {
                info = await _serverInfoProvider(
                    remoteDeviceId,
                    cancellationToken);
            }
            catch
            {
                info = new ServerInfo(null, null);
            }

            await WorldTransferProtocol.SendMessageAsync(
                connection,
                ServerInfoProtocol.ServerInfoResponse,
                cancellationToken);

            await WorldTransferProtocol.SendJsonAsync(
                connection,
                info,
                cancellationToken);

            DebugConsole.Log(
                $"WorldHub server info sent to {connection.Client.Client.RemoteEndPoint}.");

            return;
        }

        if (command != PeerProtocol.Ping)
        {
            await WorldTransferProtocol.SendMessageAsync(
                connection,
                PeerProtocol.Error,
                cancellationToken);

            return;
        }

        await WorldTransferProtocol.SendMessageAsync(
            connection,
            PeerProtocol.Pong,
            cancellationToken);

        command = await WorldTransferProtocol.ReceiveMessageAsync(
            connection,
            cancellationToken);

        if (command != PeerProtocol.WorldHub)
        {
            await WorldTransferProtocol.SendMessageAsync(
                connection,
                PeerProtocol.Error,
                cancellationToken);

            return;
        }

        await WorldTransferProtocol.SendMessageAsync(
            connection,
            PeerProtocol.WorldHubOk,
            cancellationToken);

        var profile = await CreateLocalProfileAsync(cancellationToken);

        await WorldTransferProtocol.SendJsonAsync(
            connection,
            profile,
            cancellationToken);

        DebugConsole.Log(
            $"WorldHub profile sent to {connection.Client.Client.RemoteEndPoint}.");
    }

    private async Task<PeerInfo> CreateLocalProfileAsync(
        CancellationToken cancellationToken)
    {
        var userName = _userNameProvider();

        if (string.IsNullOrWhiteSpace(userName))
        {
            userName = Environment.UserName;
        }

        var version = _versionProvider();

        var googleDriveStatus = _googleDriveStatusProvider();

        if (string.IsNullOrWhiteSpace(googleDriveStatus))
        {
            googleDriveStatus = "Unknown";
        }

        string? googleEmail;

        try
        {
            googleEmail = await _googleEmailProvider(cancellationToken);
        }
        catch
        {
            googleEmail = null;
        }

        return new PeerInfo(
            _deviceId,
            userName,
            Environment.MachineName,
            version,
            "Online",
            googleDriveStatus,
            googleEmail);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            _disposeCts.Cancel();
        }
        catch
        {
            // ignore
        }

        _disposeCts.Dispose();
        _networkHost.Dispose();
    }
}