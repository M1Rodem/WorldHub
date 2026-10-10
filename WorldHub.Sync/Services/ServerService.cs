using WorldHub.Core.Entities;
using WorldHub.Core.Interfaces;
using WorldHub.Core.Rules;
using WorldHub.Logging;

namespace WorldHub.Sync.Services;

public sealed class ServerService
{
    private readonly IServerRepository _serverRepository;

    public ServerService(
        IServerRepository serverRepository)
    {
        ArgumentNullException.ThrowIfNull(
            serverRepository);

        _serverRepository = serverRepository;
    }

    public async Task<Server> CreateAsync(
        string name,
        ServerDetectionResult detection,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Server name cannot be empty.",
                nameof(name));
        }

        ArgumentNullException.ThrowIfNull(detection);

        var normalizedLocalPath =
            Path.GetFullPath(detection.ServerDirectory);

        if (!Directory.Exists(normalizedLocalPath))
        {
            throw new DirectoryNotFoundException(
                $"Server directory was not found:\n{normalizedLocalPath}");
        }

        var existingServers =
            await _serverRepository.GetAllAsync(
                cancellationToken);

        var duplicateServer =
            existingServers.FirstOrDefault(
                server => ServerPathRule.IsSame(
                    server.LocalPath,
                    normalizedLocalPath));

        if (duplicateServer is not null)
        {
            throw new InvalidOperationException(
                "A Minecraft server is already registered at this path.\n\n" +
                $"Path:\n{normalizedLocalPath}");
        }

        var server = new Server
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            LocalPath = normalizedLocalPath,
            MinecraftVersion = detection.MinecraftVersion,
            Loader = detection.Loader,
            LoaderVersion = detection.LoaderVersion,
            LaunchConfiguration = detection.LaunchConfiguration,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _serverRepository.AddAsync(
            server,
            cancellationToken);

        AppLog.Success(
            $"[SERVER] Server '{server.Name}' registered at '{server.LocalPath}'");

        return server;
    }

    public Task<Server?> GetByIdAsync(
        Guid serverId,
        CancellationToken cancellationToken = default)
    {
        if (serverId == Guid.Empty)
        {
            throw new ArgumentException(
                "Server ID cannot be empty.",
                nameof(serverId));
        }

        return _serverRepository.GetByIdAsync(
            serverId,
            cancellationToken);
    }

    public Task<IReadOnlyCollection<Server>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return _serverRepository.GetAllAsync(
            cancellationToken);
    }

    public async Task UpdateAsync(
        Server server,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);

        server.UpdatedAt = DateTime.UtcNow;

        await _serverRepository.UpdateAsync(
            server,
            cancellationToken);
    }

    public async Task DeleteAsync(
        Guid serverId,
        CancellationToken cancellationToken = default)
    {
        if (serverId == Guid.Empty)
        {
            throw new ArgumentException(
                "Server ID cannot be empty.",
                nameof(serverId));
        }

        await _serverRepository.DeleteAsync(
            serverId,
            cancellationToken);

        AppLog.Log($"[SERVER] Deleted server {serverId} from application.");
    }

    private static string? NormalizeOptionalValue(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}