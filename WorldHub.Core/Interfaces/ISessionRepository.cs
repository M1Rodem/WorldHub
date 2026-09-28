using WorldHub.Core.Entities;

namespace WorldHub.Core.Interfaces;

public interface ISessionRepository
{
    Task<Session?> GetByIdAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<Session?> GetActiveByWorldIdAsync(
        Guid worldId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Session>> GetByWorldIdAsync(
        Guid worldId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Session session,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Session session,
        CancellationToken cancellationToken = default);
}