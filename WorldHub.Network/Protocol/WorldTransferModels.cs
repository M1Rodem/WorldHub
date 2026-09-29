namespace WorldHub.Network.Protocol;

public sealed record PushRequest(
    Guid WorldId,
    long SnapshotId);

public sealed record PullRequest(
    string WorldName);

public sealed record SnapshotTransferMetadata(
    Guid WorldId,
    string WorldName,
    string MinecraftVersion,
    string Loader,
    string? LoaderVersion,
    string? ModpackHash,
    long SnapshotId,
    long SnapshotVersion,
    string WorldHash,
    string Message,
    DateTime CreatedAt,
    long TotalBytes,
    int FileCount);

public sealed record SnapshotFileMetadata(
    string RelativePath,
    long Length);