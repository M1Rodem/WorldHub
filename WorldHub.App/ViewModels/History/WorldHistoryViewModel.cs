using WorldHub.App.ViewModels.Worlds;

namespace WorldHub.App.ViewModels.History;

public sealed class WorldHistoryViewModel
{
    public WorldHistoryViewModel(
        WorldViewModel world,
        IReadOnlyCollection<SnapshotViewModel> snapshots)
    {
        WorldId = world.Id;
        WorldName = world.Name;
        WorldPath = world.LocalPath;
        Snapshots = snapshots;
    }

    public Guid WorldId { get; }

    public string WorldName { get; }

    public string WorldPath { get; }

    public IReadOnlyCollection<SnapshotViewModel> Snapshots { get; }
}