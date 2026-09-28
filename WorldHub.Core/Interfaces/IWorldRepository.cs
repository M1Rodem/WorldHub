using WorldHub.Core.Entities;

namespace WorldHub.Core.Interfaces;

public interface IWorldRepository
{
    Task<World?> GetByIdAsync(
        Guid worldId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<World>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task AddAsync(
        World world,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        World world,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid worldId,
        CancellationToken cancellationToken = default);
}