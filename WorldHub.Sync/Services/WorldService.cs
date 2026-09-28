using WorldHub.Core.Entities;
using WorldHub.Core.Enums;
using WorldHub.Core.Interfaces;

namespace WorldHub.Sync.Services;

public sealed class WorldService
{
    private readonly IWorldRepository _worldRepository;

    public WorldService(IWorldRepository worldRepository)
    {
        _worldRepository = worldRepository;
    }

    public async Task<World> CreateAsync(
        string name,
        string localPath,
        string minecraftVersion,
        string loader,
        string? loaderVersion = null,
        string? modpackHash = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "World name cannot be empty.",
                nameof(name));
        }

        if (string.IsNullOrWhiteSpace(localPath))
        {
            throw new ArgumentException(
                "World path cannot be empty.",
                nameof(localPath));
        }

        if (string.IsNullOrWhiteSpace(minecraftVersion))
        {
            throw new ArgumentException(
                "Minecraft version cannot be empty.",
                nameof(minecraftVersion));
        }

        if (string.IsNullOrWhiteSpace(loader))
        {
            throw new ArgumentException(
                "Minecraft loader cannot be empty.",
                nameof(loader));
        }

        var world = new World
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            LocalPath = Path.GetFullPath(localPath),
            MinecraftVersion = minecraftVersion.Trim(),
            Loader = loader.Trim(),
            LoaderVersion = NormalizeOptionalValue(loaderVersion),
            ModpackHash = NormalizeOptionalValue(modpackHash),
            CurrentSnapshotId = 0,
            Status = WorldStatus.Ready,
            OwnerId = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _worldRepository.AddAsync(
            world,
            cancellationToken);

        return world;
    }

    public Task<World?> GetByIdAsync(
        Guid worldId,
        CancellationToken cancellationToken = default)
    {
        if (worldId == Guid.Empty)
        {
            throw new ArgumentException(
                "World ID cannot be empty.",
                nameof(worldId));
        }

        return _worldRepository.GetByIdAsync(
            worldId,
            cancellationToken);
    }

    public Task<IReadOnlyCollection<World>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return _worldRepository.GetAllAsync(
            cancellationToken);
    }

    public async Task UpdateAsync(
        World world,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(world);

        world.UpdatedAt = DateTime.UtcNow;

        await _worldRepository.UpdateAsync(
            world,
            cancellationToken);
    }

    public async Task DeleteAsync(
        Guid worldId,
        CancellationToken cancellationToken = default)
    {
        if (worldId == Guid.Empty)
        {
            throw new ArgumentException(
                "World ID cannot be empty.",
                nameof(worldId));
        }

        var world = await _worldRepository.GetByIdAsync(
            worldId,
            cancellationToken);

        if (world is null)
        {
            throw new InvalidOperationException(
                $"World with ID '{worldId}' was not found.");
        }

        if (world.Status == WorldStatus.Playing)
        {
            throw new InvalidOperationException(
                "A world cannot be deleted while it is being played.");
        }

        if (world.Status == WorldStatus.Syncing)
        {
            throw new InvalidOperationException(
                "A world cannot be deleted while it is syncing.");
        }

        if (world.Status == WorldStatus.Restoring)
        {
            throw new InvalidOperationException(
                "A world cannot be deleted while it is being restored.");
        }

        await _worldRepository.DeleteAsync(
            worldId,
            cancellationToken);
    }

    public async Task SetStatusAsync(
        Guid worldId,
        WorldStatus status,
        CancellationToken cancellationToken = default)
    {
        var world = await _worldRepository.GetByIdAsync(
            worldId,
            cancellationToken);

        if (world is null)
        {
            throw new InvalidOperationException(
                $"World with ID '{worldId}' was not found.");
        }

        world.Status = status;
        world.UpdatedAt = DateTime.UtcNow;

        await _worldRepository.UpdateAsync(
            world,
            cancellationToken);
    }

    private static string? NormalizeOptionalValue(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}