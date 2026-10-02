using WorldHub.Core.Entities;

namespace WorldHub.Core.Rules;

public static class SessionOwnershipRule
{
    public static bool CanStartSession(
        WorldOwnership? ownership,
        Guid hostPlayerId,
        long startSnapshotId)
    {
        return ownership is not null &&
               ownership.PlayerId == hostPlayerId &&
               ownership.SnapshotId == startSnapshotId;
    }
}