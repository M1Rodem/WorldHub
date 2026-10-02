using WorldHub.Core.Entities;

namespace WorldHub.Core.Rules;

public static class RemoteWorldLinkRule
{
    public static World? FindLinkedWorld(
        Guid remoteWorldId,
        string remoteWorldName,
        IReadOnlyCollection<World> worlds)
    {
        var byRemoteId = worlds.FirstOrDefault(
            w => w.RemoteWorldId == remoteWorldId);

        if (byRemoteId is not null)
        {
            return byRemoteId;
        }

        return worlds.FirstOrDefault(
            w => string.Equals(
                w.Name,
                remoteWorldName,
                StringComparison.OrdinalIgnoreCase));
    }
}