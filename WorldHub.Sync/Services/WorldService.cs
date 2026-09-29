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

        var normalizedLocalPath = Path.GetFullPath(localPath);

        var existingWorlds = await _worldRepository.GetAllAsync(
            cancellationToken);

        var existingWorld = existingWorlds.FirstOrDefault(
            world => string.Equals(
                Path.GetFullPath(world.LocalPath),
                normalizedLocalPath,
                StringComparison.OrdinalIgnoreCase));

        if (existingWorld is not null)
        {
            throw new InvalidOperationException(
                $"Этот Minecraft-мир уже зарегистрирован в WorldHub.\n\n" +
                $"Путь:\n{normalizedLocalPath}");
        }

        var world = new World
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            LocalPath = normalizedLocalPath,
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