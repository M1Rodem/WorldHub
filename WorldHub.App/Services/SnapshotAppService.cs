using WorldHub.App.ViewModels;
using WorldHub.Core.Entities;
using WorldHub.Infrastructure.FileSystem;
using WorldHub.Infrastructure.Storage;
using WorldHub.Sync.Services;

namespace WorldHub.App.Services;

public sealed class SnapshotAppService
{
    private readonly SnapshotService _snapshotService;

    public SnapshotAppService(
        SnapshotService snapshotService)
    {
        _snapshotService = snapshotService;
    }

    public async Task<SnapshotViewModel> CreateSnapshotAsync(
        World world,
        Guid authorId,
        string message,
        CancellationToken cancellationToken = default)
    {
        var snapshot =
            await _snapshotService.CreateAsync(
                world,
                authorId,
                message,
                cancellationToken);

        return new SnapshotViewModel(snapshot);
    }

    public async Task<SnapshotViewModel?> CreateSnapshotIfChangedAsync(
        World world,
        Guid authorId,
        string message,
        CancellationToken cancellationToken = default)
    {
        var snapshot =
            await _snapshotService.CreateIfChangedAsync(
                world,
                authorId,
                message,
                cancellationToken);

        return snapshot is null
            ? null
            : new SnapshotViewModel(snapshot);
    }

    public async Task<IReadOnlyCollection<SnapshotViewModel>> GetHistoryAsync(
        Guid worldId,
        CancellationToken cancellationToken = default)
    {
        var snapshots =
            await _snapshotService.GetHistoryAsync(
                worldId,
                cancellationToken);

        return snapshots
            .OrderByDescending(x => x.Version)
            .Select(static snapshot =>
                new SnapshotViewModel(snapshot))
            .ToArray();
    }

    public async Task RestoreAsync(
        World world,
        long snapshotId,
        CancellationToken cancellationToken = default)
    {
        await _snapshotService.RestoreAsync(
            world,
            snapshotId,
            cancellationToken);
    }
}