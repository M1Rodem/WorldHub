using WorldHub.Core.Interfaces;
using WorldHub.Core.Rules;

namespace WorldHub.Sync.Services;

public sealed class WorldDeletionService
{
    private readonly IWorldRepository _worldRepository;
    private readonly IWorldOwnershipRepository _ownershipRepository;
    private readonly SnapshotService _snapshotService;

    public WorldDeletionService(
        IWorldRepository worldRepository,
        IWorldOwnershipRepository ownershipRepository,
        SnapshotService snapshotService)
    {
        _worldRepository = worldRepository;
        _ownershipRepository = ownershipRepository;
        _snapshotService = snapshotService;
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

        var world =
            await _worldRepository.GetByIdAsync(
                worldId,
                cancellationToken);

        if (world is null)
        {
            throw new InvalidOperationException(
                $"World with ID '{worldId}' was not found.");
        }

        if (!WorldDeletionAllowedRule.IsAllowed(world))
        {
            throw new InvalidOperationException(
                $"A world cannot be deleted while its status is '{world.Status}'.");
        }

        // Удаляем физические snapshot-копии и их записи.
        await _snapshotService.DeleteByWorldIdAsync(
            worldId,
            cancellationToken);

        // Удаляем WorldHub ownership/link.
        await _ownershipRepository.DeleteByWorldIdAsync(
            worldId,
            cancellationToken);

        // Удаляем только запись мира из WorldHub.
        // world.LocalPath намеренно нигде не используется для удаления.
        await _worldRepository.DeleteAsync(
            worldId,
            cancellationToken);
    }
}