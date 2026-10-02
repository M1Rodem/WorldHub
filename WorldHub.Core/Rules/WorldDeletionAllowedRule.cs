using WorldHub.Core.Entities;
using WorldHub.Core.Enums;

namespace WorldHub.Core.Rules;

public static class WorldDeletionAllowedRule
{
    public static bool IsAllowed(World world)
    {
        return world.Status is
            WorldStatus.Ready or
            WorldStatus.Error;
    }
}