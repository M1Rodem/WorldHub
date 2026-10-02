using WorldHub.Core.Entities;

namespace WorldHub.Core.Rules;

public static class DuplicateWorldPathRule
{
    public static bool IsUsedByAnotherWorld(
        Guid worldId,
        string normalizedPath,
        IReadOnlyCollection<World> worlds)
    {
        return worlds.Any(world =>
            world.Id != worldId &&
            string.Equals(
                Path.GetFullPath(world.LocalPath),
                normalizedPath,
                StringComparison.OrdinalIgnoreCase));
    }
}