using WorldHub.Core.Entities;

namespace WorldHub.Core.Interfaces;

public interface IWorldHubServerRepository
{
    Task<WorldHubServer?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorldHubServer>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task AddAsync(
        WorldHubServer worldHubServer,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        WorldHubServer worldHubServer,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}