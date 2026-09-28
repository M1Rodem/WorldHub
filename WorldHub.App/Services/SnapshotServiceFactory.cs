using System.IO;
using WorldHub.Infrastructure.FileSystem;
using WorldHub.Infrastructure.Storage;
using WorldHub.Sync.Services;

namespace WorldHub.App.Services;

public static class SnapshotServiceFactory
{
    public static SnapshotService Create()
    {
        var dataPath = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "WorldHub",
            "data");

        var worldsPath = Path.Combine(
            dataPath,
            "worlds");

        var snapshotsPath = Path.Combine(
            dataPath,
            "snapshots");

        var worldRepository =
            new JsonWorldRepository(
                worldsPath);

        var snapshotRepository =
            new JsonSnapshotRepository(
                snapshotsPath);

        var snapshotStorage =
            new SnapshotFileStorage(
                snapshotsPath);

        var worldHashService =
            new WorldHashService();

        return new SnapshotService(
            snapshotStorage,
            snapshotRepository,
            worldHashService,
            worldRepository);
    }
}