using System.IO;
using System.Net.Sockets;
using WorldHub.Logging;
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
    private readonly Func<PeerInfo, string, CancellationToken, Task>? _remoteProfileReceivedHandler;
    private readonly Func<InviteRequest, CancellationToken, Task<bool>>? _inviteHandler;
    private readonly Func<ServerDeletedNotification, CancellationToken, Task>? _serverDeletedHandler;
    private readonly Func<ParticipantLeftNotification, CancellationToken, Task>? _participantLeftHandler;
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
        Func<PeerInfo, string, CancellationToken, Task>? remoteProfileReceivedHandler = null,
        Func<InviteRequest, CancellationToken, Task<bool>>? inviteHandler = null,
        Func<ServerDeletedNotification, CancellationToken, Task>? serverDeletedHandler = null,
        Func<ParticipantLeftNotification, CancellationToken, Task>? participantLeftHandler = null,
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
        _remoteProfileReceivedHandler = remoteProfileReceivedHandler;
        _inviteHandler = inviteHandler;
        _serverDeletedHandler = serverDeletedHandler;
        _participantLeftHandler = participantLeftHandler;
        Port = port;

        _networkHost = new NetworkHost(
            networkService,
            port,
            HandleConnectionAsync,
            AppLog.Log,
            message => AppLog.Error(message));
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
            AppLog.Log(
                $"WorldHub TCP timeout for {host}.");

            return PeerCheckResult.TcpUnavailable();
        }
        catch (Exception exception)
        {
            AppLog.Log(
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
                AppLog.Log(
                    $"WorldHub handshake timeout for {host}.");

                return PeerCheckResult.HandshakeFailed();
            }
            catch (Exception exception)
            {
                AppLog.Log(
                    $"WorldHub handshake failed for {host}: {exception.Message}");

                return PeerCheckResult.HandshakeFailed();
            }

            try
            {
                AppLog.Log($"[NET] → PING to {host}");

                await WorldTransferProtocol.SendMessageAsync(
                    connection,
                    PeerProtocol.Ping,
                    token);

                var pong = await WorldTransferProtocol.ReceiveMessageAsync(
                    connection,
                    token);

                AppLog.Log($"[NET] ← {pong} from {host}");

                if (pong != PeerProtocol.Pong)
                {
                    throw new InvalidDataException(
                        $"Expected PONG, received '{pong}'.");
                }

                AppLog.Log($"[NET] → WORLDHUB to {host}");

                await WorldTransferProtocol.SendMessageAsync(
                    connection,
                    PeerProtocol.WorldHub,
                    token);

                var response = await WorldTransferProtocol.ReceiveMessageAsync(
                    connection,
                    token);

                AppLog.Log($"[NET] ← {response} from {host}");

                if (response != PeerProtocol.WorldHubOk)
                {
                    throw new InvalidDataException(
                        $"Expected WORLDHUB_OK, received '{response}'.");
                }

                var profile =
                    await WorldTransferProtocol.ReceiveJsonAsync<PeerInfo>(
                        connection,
                        token);

                AppLog.Log(
                    $"[NET] ← PeerInfo from {host}: " +
                    $"user={profile.UserName}, pc={profile.PcName}, " +
                    $"version={profile.WorldHubVersion}, " +
                    $"email={profile.GoogleEmail ?? "(none)"}, " +
                    $"drive={profile.GoogleDriveStatus}");

                try
                {
                    var localProfile = await CreateLocalProfileAsync(token);

                    AppLog.Log(
                        $"[NET] → PeerInfo to {host}: " +
                        $"user={localProfile.UserName}, pc={localProfile.PcName}, " +
                        $"email={localProfile.GoogleEmail ?? "(none)"}");

                    await WorldTransferProtocol.SendJsonAsync(
                        connection,
                        localProfile,
                        token);
                }
                catch
                {
                    // Игнорируем: если удалённый пир старой версии сразу закрыл соединение
                }

                return PeerCheckResult.ProfileReceived(profile);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                AppLog.Log(
                    $"WorldHub protocol timeout for {host}.");

                return PeerCheckResult.ProtocolFailed();
            }
            catch (Exception exception)
            {
                AppLog.Log(
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

    public async Task<bool> SendInviteAsync(
        string host,
        InviteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentNullException.ThrowIfNull(request);

        using var linkedCts =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _disposeCts.Token);

        using var timeoutCts =
            CancellationTokenSource.CreateLinkedTokenSource(
                linkedCts.Token);

        timeoutCts.CancelAfter(TimeSpan.FromSeconds(60));

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
            AppLog.Warning($"[INVITE] Connection timeout to {host}.");
            throw new TimeoutException($"Таймаут подключения к {host}.");
        }
        catch (Exception exception)
        {
            AppLog.Warning($"[INVITE] Connection failed to {host}: {exception.Message}");
            throw;
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

                AppLog.Log(
                    $"[INVITE] → WORLDHUB_INVITE to {host} for server '{request.ServerName}'");

                await WorldTransferProtocol.SendMessageAsync(
                    connection,
                    InviteProtocol.InviteRequestCommand,
                    token);

                await WorldTransferProtocol.SendJsonAsync(
                    connection,
                    request,
                    token);

                var responseCommand = await WorldTransferProtocol.ReceiveMessageAsync(
                    connection,
                    token);

                if (responseCommand != InviteProtocol.InviteResponseCommand)
                {
                    AppLog.Warning(
                        $"[INVITE] Unexpected response '{responseCommand}' from {host}.");
                    return false;
                }

                var response = await WorldTransferProtocol.ReceiveJsonAsync<InviteResponse>(
                    connection,
                    token);

                AppLog.Log(
                    $"[INVITE] ← InviteResponse from {host}: Accepted={response.Accepted}");

                return response.Accepted;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                AppLog.Warning(
                    $"[INVITE] Waiting for response from {host} timed out.");
                throw new TimeoutException("Друг не ответил на приглашение за 60 секунд.");
            }
            catch (Exception exception)
            {
                AppLog.Error(
                    $"[INVITE] SendInvite failed to {host}: {exception.Message}",
                    exception);
                throw;
            }
        }
    }

    public async Task NotifyServerDeletedAsync(
        string host,
        ServerDeletedNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentNullException.ThrowIfNull(notification);

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _disposeCts.Token);

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var finalCts = CancellationTokenSource.CreateLinkedTokenSource(
            linkedCts.Token,
            timeoutCts.Token);

        var token = finalCts.Token;

        try
        {
            var connection = await _networkProvider.ConnectAsync(
                host,
                Port,
                token);

            await using (connection)
            {
                await NetworkHandshake.SendHelloAsync(
                    connection,
                    token);

                await NetworkHandshake.WaitForServerHelloAsync(
                    connection,
                    token);

                AppLog.Log(
                    $"[NET] → WORLDHUB_SERVER_DELETED to {host} for server '{notification.ServerName}'");

                await WorldTransferProtocol.SendMessageAsync(
                    connection,
                    InviteProtocol.ServerDeletedCommand,
                    token);

                await WorldTransferProtocol.SendJsonAsync(
                    connection,
                    notification,
                    token);

                var responseCommand = await WorldTransferProtocol.ReceiveMessageAsync(
                    connection,
                    token);

                if (responseCommand == InviteProtocol.ServerDeletedOkCommand)
                {
                    AppLog.Success(
                        $"[NET] ← WORLDHUB_SERVER_DELETED_OK from {host}");
                }
            }
        }
        catch (Exception exception)
        {
            AppLog.Warning(
                $"[NET] Failed to notify {host} of server deletion: {exception.Message}");
        }
    }

    public async Task NotifyParticipantLeftAsync(
        string host,
        ParticipantLeftNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentNullException.ThrowIfNull(notification);

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _disposeCts.Token);

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var finalCts = CancellationTokenSource.CreateLinkedTokenSource(
            linkedCts.Token,
            timeoutCts.Token);

        var token = finalCts.Token;

        try
        {
            var connection = await _networkProvider.ConnectAsync(
                host,
                Port,
                token);

            await using (connection)
            {
                await NetworkHandshake.SendHelloAsync(
                    connection,
                    token);

                await NetworkHandshake.WaitForServerHelloAsync(
                    connection,
                    token);

                AppLog.Log(
                    $"[NET] → WORLDHUB_PARTICIPANT_LEFT to {host} ({notification.ParticipantUserName} left '{notification.ServerName}')");

                await WorldTransferProtocol.SendMessageAsync(
                    connection,
                    InviteProtocol.ParticipantLeftCommand,
                    token);

                await WorldTransferProtocol.SendJsonAsync(
                    connection,
                    notification,
                    token);

                var responseCommand = await WorldTransferProtocol.ReceiveMessageAsync(
                    connection,
                    token);

                if (responseCommand == InviteProtocol.ParticipantLeftOkCommand)
                {
                    AppLog.Success(
                        $"[NET] ← WORLDHUB_PARTICIPANT_LEFT_OK from {host}");
                }
            }
        }
        catch (Exception exception)
        {
            AppLog.Warning(
                $"[NET] Failed to notify {host} that participant left: {exception.Message}");
        }
    }

    private async Task HandleConnectionAsync(
        NetworkConnection connection,
        CancellationToken cancellationToken)
    {
        var command = await WorldTransferProtocol.ReceiveMessageAsync(
            connection,
            cancellationToken);

        // Ветка WORLDHUB_INVITE — без PING/PONG.
        if (command == InviteProtocol.InviteRequestCommand)
        {
            var remoteEndPoint = connection.Client.Client.RemoteEndPoint?.ToString() ?? "?";
            AppLog.Log($"[NET] ← WORLDHUB_INVITE from {remoteEndPoint}");

            var inviteRequest = await WorldTransferProtocol.ReceiveJsonAsync<InviteRequest>(
                connection,
                cancellationToken);

            AppLog.Log(
                $"[INVITE] Received invite to server '{inviteRequest.ServerName}' from {inviteRequest.OwnerUserName} ({remoteEndPoint})");

            bool accepted = false;
            if (_inviteHandler is not null)
            {
                try
                {
                    accepted = await _inviteHandler(
                        inviteRequest,
                        cancellationToken);
                }
                catch (Exception exception)
                {
                    AppLog.Error(
                        $"[INVITE] Handler error: {exception.Message}",
                        exception);
                    accepted = false;
                }
            }

            var inviteResponse = new InviteResponse(accepted);

            await WorldTransferProtocol.SendMessageAsync(
                connection,
                InviteProtocol.InviteResponseCommand,
                cancellationToken);

            await WorldTransferProtocol.SendJsonAsync(
                connection,
                inviteResponse,
                cancellationToken);

            AppLog.Log(
                $"[INVITE] → WORLDHUB_INVITE_RESPONSE to {remoteEndPoint}: Accepted={accepted}");

            return;
        }

        // Ветка WORLDHUB_SERVER_DELETED.
        if (command == InviteProtocol.ServerDeletedCommand)
        {
            var remoteEndPoint = connection.Client.Client.RemoteEndPoint?.ToString() ?? "?";
            AppLog.Log($"[NET] ← WORLDHUB_SERVER_DELETED from {remoteEndPoint}");

            var notification = await WorldTransferProtocol.ReceiveJsonAsync<ServerDeletedNotification>(
                connection,
                cancellationToken);

            AppLog.Log(
                $"[SYNC] Server '{notification.ServerName}' deleted by host ({notification.HostDeviceId}).");

            if (_serverDeletedHandler is not null)
            {
                try
                {
                    await _serverDeletedHandler(
                        notification,
                        cancellationToken);
                }
                catch (Exception exception)
                {
                    AppLog.Error(
                        $"[SYNC] ServerDeletedHandler error: {exception.Message}",
                        exception);
                }
            }

            await WorldTransferProtocol.SendMessageAsync(
                connection,
                InviteProtocol.ServerDeletedOkCommand,
                cancellationToken);

            return;
        }

        // Ветка WORLDHUB_PARTICIPANT_LEFT.
        if (command == InviteProtocol.ParticipantLeftCommand)
        {
            var remoteEndPoint = connection.Client.Client.RemoteEndPoint?.ToString() ?? "?";
            AppLog.Log($"[NET] ← WORLDHUB_PARTICIPANT_LEFT from {remoteEndPoint}");

            var notification = await WorldTransferProtocol.ReceiveJsonAsync<ParticipantLeftNotification>(
                connection,
                cancellationToken);

            AppLog.Log(
                $"[SYNC] Participant '{notification.ParticipantUserName}' left server '{notification.ServerName}'.");

            if (_participantLeftHandler is not null)
            {
                try
                {
                    await _participantLeftHandler(
                        notification,
                        cancellationToken);
                }
                catch (Exception exception)
                {
                    AppLog.Error(
                        $"[SYNC] ParticipantLeftHandler error: {exception.Message}",
                        exception);
                }
            }

            await WorldTransferProtocol.SendMessageAsync(
                connection,
                InviteProtocol.ParticipantLeftOkCommand,
                cancellationToken);

            return;
        }

        // Ветка WORLDHUB_SERVER_INFO — без PING/PONG.
        if (command == ServerInfoProtocol.ServerInfoRequest)
        {
            var remoteEndPoint = connection.Client.Client.RemoteEndPoint?.ToString() ?? "?";

            var remoteDeviceId = await WorldTransferProtocol.ReceiveMessageAsync(
                connection,
                cancellationToken);

            AppLog.Log(
                $"[NET] ← WORLDHUB_SERVER_INFO from {remoteEndPoint}, " +
                $"requested DeviceId={remoteDeviceId}");

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

            AppLog.Log(
                $"[NET] → WORLDHUB_SERVER_INFO_OK to {remoteEndPoint}: " +
                $"folderId={info.GoogleDriveFolderId ?? "(none)"}, " +
                $"owner={info.GoogleDriveOwnerEmail ?? "(none)"}");

            await WorldTransferProtocol.SendMessageAsync(
                connection,
                ServerInfoProtocol.ServerInfoResponse,
                cancellationToken);

            await WorldTransferProtocol.SendJsonAsync(
                connection,
                info,
                cancellationToken);

            return;
        }

        var incomingEndPoint = connection.Client.Client.RemoteEndPoint?.ToString() ?? "?";

        if (command != PeerProtocol.Ping)
        {
            AppLog.Warning(
                $"[NET] ← Unexpected command '{command}' from {incomingEndPoint}, " +
                $"responding ERROR.");

            await WorldTransferProtocol.SendMessageAsync(
                connection,
                PeerProtocol.Error,
                cancellationToken);

            return;
        }

        AppLog.Log($"[NET] ← PING from {incomingEndPoint}");
        AppLog.Log($"[NET] → PONG to {incomingEndPoint}");

        await WorldTransferProtocol.SendMessageAsync(
            connection,
            PeerProtocol.Pong,
            cancellationToken);

        command = await WorldTransferProtocol.ReceiveMessageAsync(
            connection,
            cancellationToken);

        if (command != PeerProtocol.WorldHub)
        {
            AppLog.Warning(
                $"[NET] ← Unexpected command '{command}' from {incomingEndPoint}, " +
                $"responding ERROR.");

            await WorldTransferProtocol.SendMessageAsync(
                connection,
                PeerProtocol.Error,
                cancellationToken);

            return;
        }

        AppLog.Log($"[NET] ← WORLDHUB from {incomingEndPoint}");
        AppLog.Log($"[NET] → WORLDHUB_OK to {incomingEndPoint}");

        await WorldTransferProtocol.SendMessageAsync(
            connection,
            PeerProtocol.WorldHubOk,
            cancellationToken);

        var profile = await CreateLocalProfileAsync(cancellationToken);

        AppLog.Log(
            $"[NET] → PeerInfo to {incomingEndPoint}: " +
            $"user={profile.UserName}, pc={profile.PcName}, " +
            $"email={profile.GoogleEmail ?? "(none)"}, " +
            $"drive={profile.GoogleDriveStatus}");

        await WorldTransferProtocol.SendJsonAsync(
            connection,
            profile,
            cancellationToken);

        if (_remoteProfileReceivedHandler is not null)
        {
            try
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    timeoutCts.Token);

                var callerProfile = await WorldTransferProtocol.ReceiveJsonAsync<PeerInfo>(
                    connection,
                    linkedCts.Token);

                if (callerProfile is not null)
                {
                    var remoteIp = string.Empty;
                    if (connection.Client.Client.RemoteEndPoint is System.Net.IPEndPoint endPoint)
                    {
                        remoteIp = endPoint.Address.ToString();
                    }

                    await _remoteProfileReceivedHandler(
                        callerProfile,
                        remoteIp,
                        cancellationToken);
                }
            }
            catch
            {
                // Игнорируем: если клиент старой версии не отправил ответный профиль
            }
        }
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

        AppLog.Log(
            $"[NET] Local profile: {userName} / {Environment.MachineName} / " +
            $"v{version} / email: {googleEmail ?? "(none)"} / drive: {googleDriveStatus}");

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