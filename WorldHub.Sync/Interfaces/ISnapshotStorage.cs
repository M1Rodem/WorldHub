using WorldHub.Core.Entities;

namespace WorldHub.Sync.Interfaces;

public interface ISnapshotStorage
{
    Task<string> CreateAsync(
        World world,
        Snapshot snapshot,
        CancellationToken cancellationToken = default);

    Task RestoreAsync(
        Snapshot snapshot,
        string targetPath,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Snapshot snapshot,
        CancellationToken cancellationToken = default);
}