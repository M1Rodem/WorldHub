using WorldHub.Core.Entities;
using WorldHub.Core.Interfaces;

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

        return worldHubServer;
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

    public Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return _repository.DeleteAsync(
            id,
            cancellationToken);
    }

    /// <summary>
    /// Удаляет WorldHub-сервер и отвязывает от него Minecraft-серверы.
    /// Папка Google Drive НЕ удаляется — её удаление остаётся на пользователе.
    /// </summary>
    public async Task DeleteWithCleanupAsync(
        Guid worldHubServerId,
        ServerService serverService,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serverService);

        // 1. Отвязать Minecraft-серверы.
        var servers = await serverService.GetAllAsync(cancellationToken);

        foreach (var server in servers)
        {
            if (server.WorldHubServerId == worldHubServerId)
            {
                server.WorldHubServerId = null;
                await serverService.UpdateAsync(server, cancellationToken);
            }
        }

        // 2. Удалить WorldHub-сервер.
        await _repository.DeleteAsync(
            worldHubServerId,
            cancellationToken);
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