using WorldHub.Core.Entities;

namespace WorldHub.App.ViewModels;

public sealed class WorldViewModel
{
    public WorldViewModel(World world)
    {
        Id = world.Id;
        Name = world.Name;
        LocalPath = world.LocalPath;
        MinecraftVersion = world.MinecraftVersion;
        Loader = world.Loader;
        LoaderVersion = world.LoaderVersion;
        CurrentSnapshotId = world.CurrentSnapshotId;
    }

    public Guid Id { get; }

    public string Name { get; }

    public string LocalPath { get; }

    public string MinecraftVersion { get; }

    public string Loader { get; }

    public string? LoaderVersion { get; }

    public long CurrentSnapshotId { get; }

    public string VersionText =>
        CurrentSnapshotId > 0
            ? $"Версия #{CurrentSnapshotId}"
            : "Нет snapshot";
}