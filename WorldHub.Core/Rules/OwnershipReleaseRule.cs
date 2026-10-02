using WorldHub.Core.Entities;

namespace WorldHub.Core.Rules;

public static class OwnershipReleaseRule
{
    public static bool CanRelease(
        WorldOwnership? ownership,
        Guid playerId)
    {
        return ownership is not null &&
               ownership.PlayerId == playerId;
    }
}