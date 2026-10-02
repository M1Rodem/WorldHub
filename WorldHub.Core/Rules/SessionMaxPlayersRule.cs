namespace WorldHub.Core.Rules;

public static class SessionMaxPlayersRule
{
    public const int MaxPlayers = 2;

    public static bool IsFull(int currentPlayerCount)
    {
        return currentPlayerCount >= MaxPlayers;
    }
}