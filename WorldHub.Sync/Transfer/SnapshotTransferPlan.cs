using WorldHub.Core.Entities;

namespace WorldHub.Sync.Transfer;

public sealed class SnapshotTransferPlan
{
    public required World World { get; init; }
    public required IReadOnlyList<SnapshotTransferItem> Items { get; init; }
    public required long TotalBytes { get; init; }
}

public sealed class SnapshotTransferItem
{
    public required Snapshot Snapshot { get; init; }
    public required IReadOnlyList<TransferFileItem> Files { get; init; }
    public required long TotalBytes { get; init; }
}

public sealed class TransferFileItem
{
    public required string FullPath { get; init; }
    public required string RelativePath { get; init; }
    public required long Length { get; init; }
}

public sealed class StagedSnapshot
{
    public required long SnapshotId { get; init; }
    public required long SnapshotVersion { get; init; }
    public required long? ParentSnapshotId { get; init; }
    public required Guid AuthorId { get; init; }
    public required string WorldHash { get; init; }
    public required string Message { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required string StagedPath { get; init; }
}