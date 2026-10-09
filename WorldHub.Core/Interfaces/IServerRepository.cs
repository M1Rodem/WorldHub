using WorldHub.Core.Entities;

namespace WorldHub.Core.Interfaces;

public interface IServerRepository
{
    Task<Server?> GetByIdAsync(
        Guid serverId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Server>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Server server,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Server server,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid serverId,
        CancellationToken cancellationToken = default);
}