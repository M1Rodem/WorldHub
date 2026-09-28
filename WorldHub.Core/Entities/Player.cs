namespace WorldHub.Core.Entities;

public sealed class Player
{
    public Guid Id { get; init; }

    public required string Name { get; set; }

    public DateTime CreatedAt { get; init; }

    public DateTime LastSeenAt { get; set; }

    public bool IsOnline { get; set; }
}