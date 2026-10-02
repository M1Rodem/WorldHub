namespace WorldHub.App.Services.Identity;
public sealed class LocalPlayerIdentity
{
    public Guid PlayerId { get; }

    public LocalPlayerIdentity(Guid playerId)
    {
        if (playerId == Guid.Empty)
        {
            throw new ArgumentException(
                "Player ID cannot be empty.",
                nameof(playerId));
        }

        PlayerId = playerId;
    }
}