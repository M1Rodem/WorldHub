using WorldHub.Core.Entities;

namespace WorldHub.App.ViewModels.History;

public sealed class SnapshotViewModel
{
    public SnapshotViewModel(
        Snapshot snapshot)
    {
        Id = snapshot.Id;
        WorldId = snapshot.WorldId;
        Version = snapshot.Version;
        ParentSnapshotId = snapshot.ParentSnapshotId;
        AuthorId = snapshot.AuthorId;
        Message = snapshot.Message;
        WorldHash = snapshot.WorldHash;
        StoragePath = snapshot.StoragePath;
        CreatedAt = snapshot.CreatedAt;
    }

    public long Id { get; }

    public Guid WorldId { get; }

    public long Version { get; }

    public long? ParentSnapshotId { get; }

    public Guid AuthorId { get; }

    public string Message { get; }

    public string WorldHash { get; }

    public string StoragePath { get; }

    public DateTime CreatedAt { get; }

    public string VersionText =>
        $"Snapshot #{Version}";

    public string CreatedText =>
        CreatedAt.ToLocalTime()
            .ToString("dd.MM.yyyy HH:mm:ss");

    public string ShortHash =>
        WorldHash.Length > 12
            ? WorldHash[..12]
            : WorldHash;
}