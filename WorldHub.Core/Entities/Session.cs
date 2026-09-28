using WorldHub.Core.Enums;

namespace WorldHub.Core.Entities;

public sealed class Session
{
    public Guid Id { get; init; }

    public Guid WorldId { get; init; }

    public Guid HostPlayerId { get; init; }

    public SessionStatus Status { get; set; }

    public DateTime StartedAt { get; init; }

    public DateTime? EndedAt { get; set; }

    public long StartSnapshotId { get; init; }

    public long? ResultSnapshotId { get; set; }
}