using WorldHub.Core.Entities;
using WorldHub.Core.Enums;
using WorldHub.Core.Interfaces;

namespace WorldHub.Sync.Services;

public sealed class WorldOwnershipService
{
    private readonly IWorldOwnershipRepository _repository;
    private readonly IWorldRepository _worldRepository;

    public WorldOwnershipService(
        IWorldOwnershipRepository repository,
        IWorldRepository worldRepository)
    {
        _repository = repository;
        _worldRepository = worldRepository;
    }

    public Task<WorldOwnership?> GetActiveAsync(
        Guid worldId,
        CancellationToken cancellationToken = default)
    {
        return _repository.GetActiveAsync(
            worldId,
            cancellationToken);
    }

    public async Task<WorldOwnership> AcquireAsync(
        Guid worldId,
        Guid playerId,
        long snapshotId,
        CancellationToken cancellationToken = default)
    {
        if (worldId == Guid.Empty)
        {
            throw new ArgumentException(
                "World ID cannot be empty.",
                nameof(worldId));
        }

        if (playerId == Guid.Empty)
        {
            throw new ArgumentException(
                "Player ID cannot be empty.",
                nameof(playerId));
        }

        if (snapshotId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(snapshotId),
                "Snapshot ID must be greater than zero.");
        }

        var world = await _worldRepository.GetByIdAsync(
            worldId,
            cancellationToken);

        if (world is null)
        {
            throw new InvalidOperationException(
                $"World '{worldId}' was not found.");
        }

        var activeOwnership = await _repository.GetActiveAsync(
            worldId,
            cancellationToken);

        if (activeOwnership is not null)
        {
            throw new InvalidOperationException(
                $"World is already owned by player '{activeOwnership.PlayerId}'.");
        }

        var ownership = new WorldOwnership
        {
            WorldId = worldId,
            PlayerId = playerId,
            SnapshotId = snapshotId,
            AcquiredAt = DateTime.UtcNow,
            ReleasedAt = null,
            Status = OwnershipStatus.Active
        };

        await _repository.AddAsync(
            ownership,
            cancellationToken);

        world.OwnerId = playerId;
        world.UpdatedAt = DateTime.UtcNow;

        await _worldRepository.UpdateAsync(
            world,
            cancellationToken);

        return ownership;
    }

    public async Task ReleaseAsync(
        Guid worldId,
        Guid playerId,
        CancellationToken cancellationToken = default)
    {
        var ownership = await _repository.GetActiveAsync(
            worldId,
            cancellationToken);

        if (ownership is null)
        {
            throw new InvalidOperationException(
                "World does not have an active owner.");
        }

        if (ownership.PlayerId != playerId)
        {
            throw new InvalidOperationException(
                "Only the current owner can release world ownership.");
        }

        ownership.Status = OwnershipStatus.Released;
        ownership.ReleasedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(
            ownership,
            cancellationToken);

        var world = await _worldRepository.GetByIdAsync(
            worldId,
            cancellationToken);

        if (world is not null)
        {
            world.OwnerId = null;
            world.UpdatedAt = DateTime.UtcNow;

            await _worldRepository.UpdateAsync(
                world,
                cancellationToken);
        }
    }
}