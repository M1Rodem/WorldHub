using WorldHub.App.ViewModels.Worlds;
using WorldHub.Core.Entities;
using WorldHub.Sync.Services;

namespace WorldHub.App.Services.Application;

public sealed class WorldAppService
{
    private readonly WorldService _worldService;
    private readonly SnapshotService _snapshotService;
    public WorldAppService(
        WorldService worldService,
        SnapshotService snapshotService)
    {
        _worldService = worldService;
        _snapshotService = snapshotService;
    }

    public async Task<WorldViewModel> AddWorldAsync(
        string name,
        string localPath,
        string minecraftVersion,
        string loader,
        string? loaderVersion = null,
        CancellationToken cancellationToken = default)
    {
        var world = await _worldService.CreateAsync(
            name,
            localPath,
            minecraftVersion,
            loader,
            loaderVersion,
            cancellationToken: cancellationToken);

        return new WorldViewModel(world);
    }

    public Task<World?> GetWorldByIdAsync(
        Guid worldId,
        CancellationToken cancellationToken = default)
    {
        return _worldService.GetByIdAsync(
            worldId,
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<WorldViewModel>> GetWorldsAsync(
    CancellationToken cancellationToken = default)
    {
        var worlds = await _worldService.GetAllAsync(
            cancellationToken);

        var result = new List<WorldViewModel>();

        foreach (var world in worlds)
        {
            var currentSnapshotVersion = 0L;

            if (world.CurrentSnapshotId > 0)
            {
                var snapshot = await _snapshotService.GetByIdAsync(
                    world.CurrentSnapshotId,
                    cancellationToken);

                if (snapshot is not null)
                {
                    currentSnapshotVersion = snapshot.Version;
                }
            }

            result.Add(
                new WorldViewModel(
                    world,
                    currentSnapshotVersion));
        }

        return result;
    }
}