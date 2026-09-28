using WorldHub.App.ViewModels;
using WorldHub.Core.Entities;
using WorldHub.Sync.Services;

namespace WorldHub.App.Services;

public sealed class WorldAppService
{
    private readonly WorldService _worldService;

    public WorldAppService(WorldService worldService)
    {
        _worldService = worldService;
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

        return worlds
            .Select(static world =>
                new WorldViewModel(world))
            .ToArray();
    }
}