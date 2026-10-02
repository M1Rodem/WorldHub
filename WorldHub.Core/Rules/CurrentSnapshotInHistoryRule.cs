using WorldHub.Core.Entities;

namespace WorldHub.Core.Rules;

public static class CurrentSnapshotInHistoryRule
{
    public static bool IsSatisfied(World world, IReadOnlyCollection<Snapshot> snapshots)
    {
        return snapshots.Any(s => s.Id == world.CurrentSnapshotId);
    }
}