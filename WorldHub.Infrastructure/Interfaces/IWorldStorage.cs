using WorldHub.Core.Entities;

namespace WorldHub.Infrastructure.Interfaces;

public interface IWorldStorage
{
    Task<bool> ExistsAsync(
        World world,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        World world,
        CancellationToken cancellationToken = default);

    Task BackupAsync(
        World world,
        CancellationToken cancellationToken = default);
}