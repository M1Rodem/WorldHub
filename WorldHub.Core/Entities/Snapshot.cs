namespace WorldHub.Core.Entities;

public sealed class Snapshot
{
    public long Id { get; init; }

    public Guid WorldId { get; init; }

    public long Version { get; init; }

    public long? ParentSnapshotId { get; init; }

    public Guid AuthorId { get; init; }

    public required string Message { get; init; }

    public required string WorldHash { get; init; }

    public required string StoragePath { get; init; }

    public DateTime CreatedAt { get; init; }
}