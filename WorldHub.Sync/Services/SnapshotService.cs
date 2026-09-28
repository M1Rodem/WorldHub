using WorldHub.Core.Entities;
using WorldHub.Core.Interfaces;
using WorldHub.Sync.Interfaces;

namespace WorldHub.Sync.Services;

public sealed class SnapshotService
{
    private readonly ISnapshotStorage _snapshotStorage;
    private readonly ISnapshotRepository _snapshotRepository;
    private readonly IWorldHashService _worldHashService;
    private readonly IWorldRepository _worldRepository;

    public SnapshotService(
        ISnapshotStorage snapshotStorage,
        ISnapshotRepository snapshotRepository,
        IWorldHashService worldHashService,
        IWorldRepository worldRepository)
    {
        _snapshotStorage = snapshotStorage;
        _snapshotRepository = snapshotRepository;
        _worldHashService = worldHashService;
        _worldRepository = worldRepository;
    }

    public async Task<bool> HasChangesAsync(
        World world,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (!Directory.Exists(world.LocalPath))
        {
            throw new DirectoryNotFoundException(
                $"Minecraft world directory was not found: {world.LocalPath}");
        }

        var latestSnapshot =
            await _snapshotRepository.GetLatestByWorldIdAsync(
                world.Id,
                cancellationToken);

        if (latestSnapshot is null)
        {
            return true;
        }

        var currentWorldHash =
            await _worldHashService.ComputeAsync(
                world.LocalPath,
                cancellationToken);

        return !string.Equals(
            currentWorldHash,
            latestSnapshot.WorldHash,
            StringComparison.OrdinalIgnoreCase);
    }

    public async Task<Snapshot> CreateAsync(
        World world,
        Guid authorId,
        string message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (authorId == Guid.Empty)
        {
            throw new ArgumentException(
                "Author ID cannot be empty.",
                nameof(authorId));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException(
                "Snapshot message cannot be empty.",
                nameof(message));
        }

        if (!Directory.Exists(world.LocalPath))
        {
            throw new DirectoryNotFoundException(
                $"Minecraft world directory was not found: {world.LocalPath}");
        }

        var snapshotVersion =
            await _snapshotRepository.GetNextVersionAsync(
                world.Id,
                cancellationToken);

        var parentSnapshot =
            await _snapshotRepository.GetLatestByWorldIdAsync(
                world.Id,
                cancellationToken);

        var worldHash =
            await _worldHashService.ComputeAsync(
                world.LocalPath,
                cancellationToken);

        var snapshot = new Snapshot
        {
            Id = snapshotVersion,
            WorldId = world.Id,
            Version = snapshotVersion,
            ParentSnapshotId = parentSnapshot?.Id,
            AuthorId = authorId,
            Message = message.Trim(),
            WorldHash = worldHash,
            StoragePath = string.Empty,
            CreatedAt = DateTime.UtcNow
        };

        var storagePath =
            await _snapshotStorage.CreateAsync(
                world,
                snapshot,
                cancellationToken);

        snapshot = new Snapshot
        {
            Id = snapshot.Id,
            WorldId = snapshot.WorldId,
            Version = snapshot.Version,
            ParentSnapshotId = snapshot.ParentSnapshotId,
            AuthorId = snapshot.AuthorId,
            Message = snapshot.Message,
            WorldHash = snapshot.WorldHash,
            StoragePath = storagePath,
            CreatedAt = snapshot.CreatedAt
        };

        await _snapshotRepository.AddAsync(
            snapshot,
            cancellationToken);

        world.CurrentSnapshotId = snapshot.Id;
        world.UpdatedAt = DateTime.UtcNow;

        await _worldRepository.UpdateAsync(
            world,
            cancellationToken);

        return snapshot;
    }

    public async Task<Snapshot?> CreateIfChangedAsync(
        World world,
        Guid authorId,
        string message,
        CancellationToken cancellationToken = default)
    {
        var hasChanges =
            await HasChangesAsync(
                world,
                cancellationToken);

        if (!hasChanges)
        {
            return null;
        }

        return await CreateAsync(
            world,
            authorId,
            message,
            cancellationToken);
    }

    public Task<Snapshot?> GetByIdAsync(
        long snapshotId,
        CancellationToken cancellationToken = default)
    {
        if (snapshotId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(snapshotId));
        }

        return _snapshotRepository.GetByIdAsync(
            snapshotId,
            cancellationToken);
    }

    public Task<IReadOnlyCollection<Snapshot>> GetHistoryAsync(
        Guid worldId,
        CancellationToken cancellationToken = default)
    {
        if (worldId == Guid.Empty)
        {
            throw new ArgumentException(
                "World ID cannot be empty.",
                nameof(worldId));
        }

        return _snapshotRepository.GetByWorldIdAsync(
            worldId,
            cancellationToken);
    }

    public async Task RestoreAsync(
        World world,
        long snapshotId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (snapshotId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(snapshotId));
        }

        var snapshot =
            await _snapshotRepository.GetByIdAsync(
                snapshotId,
                cancellationToken);

        if (snapshot is null)
        {
            throw new InvalidOperationException(
                $"Snapshot '{snapshotId}' was not found.");
        }

        if (snapshot.WorldId != world.Id)
        {
            throw new InvalidOperationException(
                "Snapshot belongs to another world.");
        }

        if (!Directory.Exists(snapshot.StoragePath))
        {
            throw new DirectoryNotFoundException(
                $"Snapshot storage was not found: {snapshot.StoragePath}");
        }

        await _snapshotStorage.RestoreAsync(
            snapshot,
            world.LocalPath,
            cancellationToken);

        world.CurrentSnapshotId = snapshot.Id;
        world.UpdatedAt = DateTime.UtcNow;

        await _worldRepository.UpdateAsync(
            world,
            cancellationToken);
    }
}