using System.Collections.Concurrent;
using WorldHub.Core.Entities;
using WorldHub.Logging;
using WorldHub.Sync.Services;

namespace WorldHub.App.Services.Network;

public sealed class WorldHubParticipantCheckService
{
    private readonly WorldHubNetworkService _networkService;
    private readonly WorldHubServerService _serverService;
    private readonly string? _localDeviceId;
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _gates = new();

    private readonly Func<string>? _userNameProvider;
    private readonly Func<string>? _versionProvider;
    private readonly Func<string>? _googleDriveStatusProvider;

    public WorldHubParticipantCheckService(
        WorldHubNetworkService networkService,
        WorldHubServerService serverService,
        string? localDeviceId = null,
        Func<string>? userNameProvider = null,
        Func<string>? versionProvider = null,
        Func<string>? googleDriveStatusProvider = null)
    {
        ArgumentNullException.ThrowIfNull(networkService);
        ArgumentNullException.ThrowIfNull(serverService);

        _networkService = networkService;
        _serverService = serverService;
        _localDeviceId = localDeviceId;
        _userNameProvider = userNameProvider;
        _versionProvider = versionProvider;
        _googleDriveStatusProvider = googleDriveStatusProvider;
    }

    private SemaphoreSlim GetGate(Guid worldHubServerId)
    {
        return _gates.GetOrAdd(
            worldHubServerId,
            _ => new SemaphoreSlim(1, 1));
    }

    public void RemoveGate(Guid worldHubServerId)
    {
        _gates.TryRemove(worldHubServerId, out _);
    }

    public async Task CheckAsync(
        Guid worldHubServerId,
        Guid participantId,
        CancellationToken cancellationToken = default)
    {
        var gate = GetGate(worldHubServerId);

        await gate.WaitAsync(cancellationToken);

        try
        {
            await CheckParticipantCoreAsync(
                worldHubServerId,
                participantId,
                cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task CheckAllAsync(
        Guid worldHubServerId,
        CancellationToken cancellationToken = default)
    {
        var gate = GetGate(worldHubServerId);

        await gate.WaitAsync(cancellationToken);

        try
        {
            var server = await _serverService.GetByIdAsync(
                worldHubServerId,
                cancellationToken);

            if (server is null)
            {
                return;
            }

            var participantIds = server.Participants
                .Select(item => item.Id)
                .ToList();

            foreach (var participantId in participantIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await CheckParticipantCoreAsync(
                        worldHubServerId,
                        participantId,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    AppLog.Error(
                        $"Participant check failed: {exception.Message}",
                        exception);
                }
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<WorldHubParticipant> AddParticipantAsync(
        Guid worldHubServerId,
        string ipAddress,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ipAddress);

        var gate = GetGate(worldHubServerId);

        await gate.WaitAsync(cancellationToken);

        try
        {
            var participant = new WorldHubParticipant
            {
                IpAddress = ipAddress.Trim()
            };

            await _serverService.AddParticipantAsync(
                worldHubServerId,
                participant,
                cancellationToken);

            return participant;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task RemoveParticipantAsync(
        Guid worldHubServerId,
        Guid participantId,
        CancellationToken cancellationToken = default)
    {
        var gate = GetGate(worldHubServerId);

        await gate.WaitAsync(cancellationToken);

        try
        {
            await _serverService.RemoveParticipantAsync(
                worldHubServerId,
                participantId,
                cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task CheckParticipantCoreAsync(
        Guid worldHubServerId,
        Guid participantId,
        CancellationToken cancellationToken)
    {
        var server = await _serverService.GetByIdAsync(
            worldHubServerId,
            cancellationToken);

        if (server is null)
        {
            return;
        }

        var participant = server.Participants
            .FirstOrDefault(item => item.Id == participantId);

        if (participant is null)
        {
            return;
        }

        AppLog.Log(
            $"[PEER] Checking participant {participant.IpAddress} ...");

        // Себя не пингуем по сети — обновляем актуальные локальные данные профиля.
        if (!string.IsNullOrWhiteSpace(_localDeviceId) &&
            !string.IsNullOrWhiteSpace(participant.DeviceId) &&
            string.Equals(
                _localDeviceId,
                participant.DeviceId,
                StringComparison.OrdinalIgnoreCase))
        {
            participant.IsPingAvailable = true;
            participant.IsWorldHubResponding = true;
            participant.LastCheckAtUtc = DateTimeOffset.UtcNow;
            participant.LastSeenAtUtc = DateTimeOffset.UtcNow;

            var currentUserName = _userNameProvider?.Invoke();
            if (!string.IsNullOrWhiteSpace(currentUserName))
            {
                participant.UserName = currentUserName;
            }

            participant.PcName = Environment.MachineName;
            participant.WorldHubStatus = "Online";

            if (_versionProvider is not null)
            {
                participant.WorldHubVersion = _versionProvider();
            }

            if (_googleDriveStatusProvider is not null)
            {
                participant.GoogleDriveStatus = _googleDriveStatusProvider();
            }

            await _serverService.UpdateAsync(
                server,
                cancellationToken);

            AppLog.Log(
                $"[SELF] {participant.IpAddress}: updated local profile (UserName: {participant.UserName}).");

            return;
        }

        PeerCheckResult result;

        try
        {
            result = await _networkService.CheckPeerAsync(
                participant.IpAddress,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }

        participant.LastCheckAtUtc = DateTimeOffset.UtcNow;

        switch (result.Outcome)
        {
            case PeerCheckOutcome.TcpUnavailable:
                participant.IsPingAvailable = false;
                participant.IsWorldHubResponding = false;

                AppLog.Log(
                    $"[PEER] {participant.IpAddress}: TCP unavailable.");
                break;

            case PeerCheckOutcome.HandshakeFailed:
            case PeerCheckOutcome.ProtocolFailed:
                participant.IsPingAvailable = true;
                participant.IsWorldHubResponding = false;

                AppLog.Log(
                    $"[PEER] {participant.IpAddress}: " +
                    $"TCP available, WorldHub not responding ({result.Outcome}).");
                break;

            case PeerCheckOutcome.ProfileReceived:
                var profile = result.Profile
                    ?? throw new InvalidOperationException(
                        "ProfileReceived без профиля.");

                participant.IsPingAvailable = true;
                participant.IsWorldHubResponding = true;
                participant.DeviceId = profile.DeviceId;
                participant.UserName = profile.UserName;
                participant.PcName = profile.PcName;
                participant.WorldHubVersion = profile.WorldHubVersion;
                participant.WorldHubStatus = profile.WorldHubStatus;
                participant.GoogleDriveStatus = profile.GoogleDriveStatus;
                participant.GoogleEmail = profile.GoogleEmail;
                participant.LastSeenAtUtc = DateTimeOffset.UtcNow;

                AppLog.Success(
                    $"[PEER] {participant.IpAddress} verified: " +
                    $"{profile.PcName} / {profile.UserName}");

                AppLog.Log(
                    $"[PEER]   DeviceId:       {profile.DeviceId}");
                AppLog.Log(
                    $"[PEER]   UserName:       {profile.UserName}");
                AppLog.Log(
                    $"[PEER]   PcName:         {profile.PcName}");
                AppLog.Log(
                    $"[PEER]   Version:        {profile.WorldHubVersion}");
                AppLog.Log(
                    $"[PEER]   DriveStatus:    {profile.GoogleDriveStatus}");
                AppLog.Log(
                    $"[PEER]   GoogleEmail:    {profile.GoogleEmail ?? "(none)"}");

                // Если у нас ещё нет FolderId — попробуем получить его у A.
                if (string.IsNullOrWhiteSpace(server.GoogleDriveFolderId))
                {
                    AppLog.Log(
                        $"[FOLDER] Requesting FolderId from {participant.IpAddress} ...");

                    var serverInfo = await _networkService.RequestServerInfoAsync(
                        participant.IpAddress,
                        cancellationToken);

                    if (serverInfo is not null &&
                        !string.IsNullOrWhiteSpace(serverInfo.GoogleDriveFolderId))
                    {
                        server.GoogleDriveFolderId = serverInfo.GoogleDriveFolderId;
                        server.GoogleDriveOwnerEmail = serverInfo.GoogleDriveOwnerEmail;

                        AppLog.Success(
                            $"[FOLDER] FolderId received: {serverInfo.GoogleDriveFolderId}");
                        AppLog.Log(
                            $"[FOLDER] Owner email: {serverInfo.GoogleDriveOwnerEmail ?? "(none)"}");
                    }
                    else
                    {
                        AppLog.Warning(
                            $"[FOLDER] No FolderId received from {participant.IpAddress}.");
                    }
                }

                break;
        }

        await _serverService.UpdateAsync(
            server,
            cancellationToken);
    }
}