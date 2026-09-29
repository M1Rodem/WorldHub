using WorldHub.Core.Entities;

namespace WorldHub.Core.Interfaces;

public interface ISnapshotRepository
{
    Task<Snapshot?> GetByIdAsync(
        long snapshotId,
        CancellationToken cancellationToken = default);

    Task<Snapshot?> GetLatestByWorldIdAsync(
        Guid worldId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Snapshot>> GetByWorldIdAsync(
        Guid worldId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Snapshot snapshot,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        long snapshotId,
        CancellationToken cancellationToken = default);
    
    Task<long> GetNextVersionAsync(
        Guid worldId,
        CancellationToken cancellationToken = default);

    Task DeleteByWorldIdAsync(
        Guid worldId,
        CancellationToken cancellationToken = default);
}