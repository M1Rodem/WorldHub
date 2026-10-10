using WorldHub.Core.Entities;
using WorldHub.Core.Interfaces;
using WorldHub.Infrastructure.Google;
using WorldHub.Logging;

namespace WorldHub.Sync.Services;

public sealed class WorldHubServerService
{
    private readonly IWorldHubServerRepository _repository;

    public WorldHubServerService(
        IWorldHubServerRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);

        _repository = repository;
    }

    public Task<WorldHubServer?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return _repository.GetByIdAsync(
            id,
            cancellationToken);
    }

    public Task<IReadOnlyList<WorldHubServer>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return _repository.GetAllAsync(
            cancellationToken);
    }

    public async Task<WorldHubServer> CreateAsync(
        string name,
        WorldHubParticipant? owner = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Название WorldHub-server не может быть пустым.",
                nameof(name));
        }

        var now = DateTime.UtcNow;

        var worldHubServer = new WorldHubServer
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            HostDeviceId = owner?.DeviceId,
            HostUserName = owner?.UserName,
            CreatedAt = now,
            UpdatedAt = now
        };

        if (owner is not null)
        {
            worldHubServer.Participants.Add(owner);
        }

        await _repository.AddAsync(
            worldHubServer,
            cancellationToken);

        AppLog.Success(
            $"[SYNC] Created WorldHub server '{worldHubServer.Name}' ({worldHubServer.Id})");

        return worldHubServer;
    }

    public async Task<WorldHubServer> CreateFromInviteAsync(
        string serverName,
        string? folderId,
        string? ownerEmail,
        string hostDeviceId,
        string hostUserName,
        List<WorldHub.Network.Protocol.InviteParticipant> participants,
        string localDeviceId,
        string localUserName,
        string localPcName,
        string localIpAddress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(serverName))
        {
            throw new ArgumentException(
                "Название WorldHub-server не может быть пустым.",
                nameof(serverName));
        }

        var now = DateTime.UtcNow;

        var server = new WorldHubServer
        {
            Id = Guid.NewGuid(),
            Name = serverName.Trim(),
            GoogleDriveFolderId = folderId,
            GoogleDriveOwnerEmail = ownerEmail,
            HostDeviceId = hostDeviceId,
            HostUserName = hostUserName,
            CreatedAt = now,
            UpdatedAt = now
        };

        if (participants is not null)
        {
            foreach (var p in participants)
            {
                if (string.Equals(p.DeviceId, localDeviceId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                server.Participants.Add(new WorldHubParticipant
                {
                    Id = Guid.NewGuid(),
                    DeviceId = p.DeviceId,
                    IpAddress = p.IpAddress,
                    UserName = p.UserName,
                    PcName = p.PcName,
                    LastSeenAtUtc = DateTimeOffset.UtcNow
                });
            }
        }

        server.Participants.Add(new WorldHubParticipant
        {
            Id = Guid.NewGuid(),
            DeviceId = localDeviceId,
            IpAddress = localIpAddress,
            UserName = localUserName,
            PcName = localPcName,
            IsPingAvailable = true,
            IsWorldHubResponding = true,
            LastCheckAtUtc = DateTimeOffset.UtcNow,
            LastSeenAtUtc = DateTimeOffset.UtcNow
        });

        await _repository.AddAsync(
            server,
            cancellationToken);

        AppLog.Success(
            $"[SYNC] Created WorldHub server '{server.Name}' ({server.Id}) from invite.");

        return server;
    }

    public async Task UpdateAsync(
        WorldHubServer worldHubServer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(worldHubServer);

        worldHubServer.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(
            worldHubServer,
            cancellationToken);
    }

    public async Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAsync(
            id,
            cancellationToken);

        AppLog.Log(
            $"[SYNC] Deleted WorldHub server {id}");
    }

    /// <summary>
    /// Удаляет WorldHub-сервер, отвязывает от него Minecraft-серверы и удаляет общую папку в Google Drive (если вызвано хостом).
    /// </summary>
    public async Task<FolderDeleteResult?> DeleteWithCleanupAsync(
        Guid worldHubServerId,
        ServerService serverService,
        GoogleDriveClient? googleDriveClient = null,
        bool deleteCloudFolder = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serverService);

        FolderDeleteResult? folderDeleteResult = null;

        // 1. Получить сервер из репозитория
        var server = await _repository.GetByIdAsync(worldHubServerId, cancellationToken);

        // 2. Если требуется удалить папку в Google Drive (только хост)
        if (deleteCloudFolder && googleDriveClient is not null && server is not null)
        {
            if (!string.IsNullOrWhiteSpace(server.GoogleDriveFolderId))
            {
                try
                {
                    AppLog.Log($"[SYNC] Попытка удаления папки Google Drive ({server.GoogleDriveFolderId}) для сервера '{server.Name}'...");
                    folderDeleteResult = await googleDriveClient.DeleteFolderAsync(
                        server.GoogleDriveFolderId,
                        cancellationToken);

                    if (folderDeleteResult.Success)
                    {
                        if (folderDeleteResult.AlreadyDeleted)
                        {
                            AppLog.Log($"[SYNC] Папка Google Drive {server.GoogleDriveFolderId} уже была удалена.");
                        }
                        else
                        {
                            AppLog.Success($"[SYNC] Папка Google Drive {server.GoogleDriveFolderId} успешно удалена.");
                        }
                    }
                    else
                    {
                        AppLog.Warning($"[SYNC] Не удалось удалить папку Google Drive: {folderDeleteResult.Message}");
                    }
                }
                catch (Exception ex)
                {
                    AppLog.Error($"[SYNC] Исключение при удалении папки Google Drive: {ex.Message}", ex);
                    folderDeleteResult = new FolderDeleteResult(false, false, ex.Message);
                }
            }
            else
            {
                AppLog.Log($"[SYNC] Сервер '{server.Name}' не имеет связанной папки Google Drive (GoogleDriveFolderId пуст).");
            }
        }

        // 3. Отвязать Minecraft-серверы.
        var servers = await serverService.GetAllAsync(cancellationToken);

        foreach (var s in servers)
        {
            if (s.WorldHubServerId == worldHubServerId)
            {
                s.WorldHubServerId = null;
                await serverService.UpdateAsync(s, cancellationToken);
            }
        }

        // 4. Удалить WorldHub-сервер.
        await _repository.DeleteAsync(
            worldHubServerId,
            cancellationToken);

        AppLog.Success($"[SYNC] WorldHub-сервер {worldHubServerId} локально удален.");

        return folderDeleteResult;
    }


    public async Task AddParticipantAsync(
        Guid worldHubServerId,
        WorldHubParticipant participant,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(participant);

        var server = await _repository.GetByIdAsync(
            worldHubServerId,
            cancellationToken);

        if (server is null)
        {
            throw new InvalidOperationException(
                "WorldHub-server не найден.");
        }

        if (string.IsNullOrWhiteSpace(participant.IpAddress))
        {
            throw new ArgumentException(
                "IP-адрес участника не может быть пустым.",
                nameof(participant));
        }

        var ipAddress = participant.IpAddress.Trim();

        if (server.Participants.Any(
                existing => string.Equals(
                    existing.IpAddress,
                    ipAddress,
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "Участник с таким IP уже добавлен.");
        }

        participant.IpAddress = ipAddress;
        server.Participants.Add(participant);

        await _repository.UpdateAsync(
            server,
            cancellationToken);

        AppLog.Success(
            $"[SYNC] Participant {ipAddress} added to WorldHub server {worldHubServerId}.");
    }

    public async Task<IReadOnlyList<WorldHubParticipant>>
        GetParticipantsAsync(
            Guid worldHubServerId,
            CancellationToken cancellationToken = default)
    {
        var server = await _repository.GetByIdAsync(
            worldHubServerId,
            cancellationToken);

        if (server is null)
        {
            throw new InvalidOperationException(
                "WorldHub-server не найден.");
        }

        return server.Participants.ToList();
    }

    public async Task RemoveParticipantAsync(
        Guid worldHubServerId,
        Guid participantId,
        CancellationToken cancellationToken = default)
    {
        var server = await _repository.GetByIdAsync(
            worldHubServerId,
            cancellationToken);

        if (server is null)
        {
            throw new InvalidOperationException(
                "WorldHub-server не найден.");
        }

        var participant = server.Participants.FirstOrDefault(
            item => item.Id == participantId);

        if (participant is null)
        {
            throw new InvalidOperationException(
                "Участник не найден.");
        }

        server.Participants.Remove(participant);

        await _repository.UpdateAsync(
            server,
            cancellationToken);

        AppLog.Log(
            $"[SYNC] Participant {participant.IpAddress} ({participantId}) removed from WorldHub server {worldHubServerId}.");
    }

    /// <summary>
    /// Ищет WorldHub-сервер, в котором есть участник с указанным DeviceId.
    /// Возвращает null, если не найдено.
    /// </summary>
    public async Task<WorldHubServer?> FindByParticipantDeviceIdAsync(
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return null;
        }

        var servers = await _repository.GetAllAsync(cancellationToken);

        return servers.FirstOrDefault(server =>
            server.Participants.Any(p =>
                !string.IsNullOrWhiteSpace(p.DeviceId) &&
                string.Equals(
                    p.DeviceId,
                    deviceId,
                    StringComparison.OrdinalIgnoreCase)));
    }

}