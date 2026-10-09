using WorldHub.Core.Entities;
using WorldHub.Core.Enums;

namespace WorldHub.Core.Rules;

public static class ServerDeletionAllowedRule
{
    public static bool IsAllowed(ServerStatus status)
    {
        return status is
            ServerStatus.Stopped or
            ServerStatus.Error;
    }
}