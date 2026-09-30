using WorldHub.Core.Enums;

namespace WorldHub.Core.Entities;

public sealed class World
{
    public Guid Id { get; init; }
    public Guid? RemoteWorldId { get; set; }

    public required string Name { get; set; }

    public required string LocalPath { get; set; }

    public required string MinecraftVersion { get; set; }

    public required string Loader { get; set; }

    public string? LoaderVersion { get; set; }

    public string? ModpackHash { get; set; }

    public long CurrentSnapshotId { get; set; }

    public WorldStatus Status { get; set; }

    public Guid? OwnerId { get; set; }

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; set; }
}