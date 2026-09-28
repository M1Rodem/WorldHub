using WorldHub.Core.Entities;

namespace WorldHub.Core.Interfaces;

public interface ISessionPlayerRepository
{
    Task<SessionPlayer?> GetAsync(
        Guid sessionId,
        Guid playerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<SessionPlayer>> GetBySessionIdAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        SessionPlayer sessionPlayer,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        SessionPlayer sessionPlayer,
        CancellationToken cancellationToken = default);
}