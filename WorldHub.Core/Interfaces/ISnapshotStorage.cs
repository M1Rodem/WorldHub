using WorldHub.Core.Entities;

namespace WorldHub.Core.Interfaces;

public interface ISnapshotStorage
{
    Task<string> CreateAsync(
        World world,
        Snapshot snapshot,
        CancellationToken cancellationToken = default);

    Task<string> ImportAsync(
        World world,
        Snapshot snapshot,
        string sourcePath,
        CancellationToken cancellationToken = default);

    Task RestoreAsync(
        Snapshot snapshot,
        string targetPath,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Snapshot snapshot,
        CancellationToken cancellationToken = default);

    Task DeleteByWorldIdAsync(
        Guid worldId,
        CancellationToken cancellationToken = default);
}