using WorldHub.Core.Enums;

namespace WorldHub.Core.Entities;

public sealed class WorldOwnership
{
    public Guid WorldId { get; init; }

    public Guid PlayerId { get; init; }

    public long SnapshotId { get; init; }

    public DateTime AcquiredAt { get; init; }

    public DateTime? ReleasedAt { get; set; }

    public OwnershipStatus Status { get; set; }
}