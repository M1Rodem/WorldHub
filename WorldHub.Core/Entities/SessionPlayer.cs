using WorldHub.Core.Enums;

namespace WorldHub.Core.Entities;

public sealed class SessionPlayer
{
    public Guid SessionId { get; init; }

    public Guid PlayerId { get; init; }

    public SessionPlayerRole Role { get; init; }

    public DateTime ConnectedAt { get; set; }

    public DateTime? DisconnectedAt { get; set; }
}