using WorldHub.Core.Entities;

namespace WorldHub.Core.Interfaces;

public interface IWorldOwnershipRepository
{
    Task<WorldOwnership?> GetActiveAsync(
        Guid worldId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        WorldOwnership ownership,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        WorldOwnership ownership,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<WorldOwnership>> GetHistoryAsync(
        Guid worldId,
        CancellationToken cancellationToken = default);
}