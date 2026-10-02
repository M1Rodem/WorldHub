using WorldHub.Core.Entities;
using WorldHub.Core.Interfaces;

namespace WorldHub.Infrastructure.Storage;

public sealed class SnapshotFileStorage : ISnapshotStorage
{
    private readonly string _snapshotsRootPath;

    public SnapshotFileStorage(string snapshotsRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotsRootPath);

        _snapshotsRootPath = snapshotsRootPath;
        Directory.CreateDirectory(_snapshotsRootPath);
    }

    public async Task<string> CreateAsync(
        World world,
        Snapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!Directory.Exists(world.LocalPath))
        {
            throw new DirectoryNotFoundException(
                $"Minecraft world directory was not found: {world.LocalPath}");
        }

        var worldDirectory = Path.GetFullPath(world.LocalPath);
        var snapshotDirectory = Path.Combine(
            _snapshotsRootPath,
            world.Id.ToString(),
            snapshot.Id.ToString());

        if (Directory.Exists(snapshotDirectory))
        {
            throw new IOException(
                $"Snapshot directory already exists: {snapshotDirectory}");
        }

        Directory.CreateDirectory(snapshotDirectory);

        try
        {
            await CopyDirectoryAsync(
                worldDirectory,
                snapshotDirectory,
                cancellationToken);

            return snapshotDirectory;
        }
        catch
        {
            if (Directory.Exists(snapshotDirectory))
            {
                Directory.Delete(snapshotDirectory, recursive: true);
            }

            throw;
        }
    }

    public async Task<string> ImportAsync(
        World world,
        Snapshot snapshot,
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        if (!Directory.Exists(sourcePath))
        {
            throw new DirectoryNotFoundException(
                $"Snapshot source directory was not found: {sourcePath}");
        }

        var sourceDirectory = Path.GetFullPath(sourcePath);

        var worldDirectory = Path.Combine(
            _snapshotsRootPath,
            world.Id.ToString());

        var snapshotDirectory = Path.Combine(
            worldDirectory,
            snapshot.Id.ToString());

        if (Directory.Exists(snapshotDirectory))
        {
            throw new IOException(
                $"Snapshot directory already exists: {snapshotDirectory}");
        }

        Directory.CreateDirectory(worldDirectory);

        try
        {
            await CopyDirectoryAsync(
                sourceDirectory,
                snapshotDirectory,
                cancellationToken);

            return snapshotDirectory;
        }
        catch
        {
            if (Directory.Exists(snapshotDirectory))
            {
                Directory.Delete(
                    snapshotDirectory,
                    recursive: true);
            }

            throw;
        }
    }

    public Task RestoreAsync(
        Snapshot snapshot,
        string targetPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        return RestoreInternalAsync(
            snapshot,
            targetPath,
            cancellationToken);
    }

    public Task DeleteAsync(
        Snapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!Directory.Exists(snapshot.StoragePath))
        {
            return Task.CompletedTask;
        }

        Directory.Delete(
            snapshot.StoragePath,
            recursive: true);

        return Task.CompletedTask;
    }

    public Task DeleteByWorldIdAsync(
        Guid worldId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var worldDirectory = Path.Combine(
            _snapshotsRootPath,
            worldId.ToString());

        if (Directory.Exists(worldDirectory))
        {
            Directory.Delete(
                worldDirectory,
                recursive: true);
        }

        return Task.CompletedTask;
    }

    private static async Task RestoreInternalAsync(
    Snapshot snapshot,
    string targetPath,
    CancellationToken cancellationToken)
    {
        if (!Directory.Exists(snapshot.StoragePath))
        {
            throw new DirectoryNotFoundException(
                $"Snapshot directory was not found: {snapshot.StoragePath}");
        }

        var fullTargetPath =
            Path.GetFullPath(targetPath);

        var backupPath =
            $"{fullTargetPath}.worldhub-restore-backup";

        if (Directory.Exists(backupPath))
        {
            Directory.Delete(
                backupPath,
                recursive: true);
        }

        var backupCreated = false;

        try
        {
            if (Directory.Exists(fullTargetPath))
            {
                await CopyDirectoryAsync(
                    fullTargetPath,
                    backupPath,
                    cancellationToken);

                backupCreated = true;

                Directory.Delete(
                    fullTargetPath,
                    recursive: true);
            }

            Directory.CreateDirectory(
                fullTargetPath);

            await CopyDirectoryAsync(
                snapshot.StoragePath,
                fullTargetPath,
                cancellationToken);

            if (backupCreated &&
                Directory.Exists(backupPath))
            {
                Directory.Delete(
                    backupPath,
                    recursive: true);
            }
        }
        catch
        {
            if (Directory.Exists(fullTargetPath))
            {
                Directory.Delete(
                    fullTargetPath,
                    recursive: true);
            }

            if (backupCreated &&
                Directory.Exists(backupPath))
            {
                Directory.Move(
                    backupPath,
                    fullTargetPath);
            }

            throw;
        }
    }

    private static async Task CopyDirectoryAsync(
        string sourceDirectory,
        string destinationDirectory,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var directory in Directory.EnumerateDirectories(
                     sourceDirectory,
                     "*",
                     SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(
                sourceDirectory,
                directory);

            var targetDirectory = Path.Combine(
                destinationDirectory,
                relativePath);

            Directory.CreateDirectory(targetDirectory);
        }

        foreach (var file in Directory.EnumerateFiles(
                     sourceDirectory,
                     "*",
                     SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(
                sourceDirectory,
                file);

            var targetFile = Path.Combine(
                destinationDirectory,
                relativePath);

            var targetDirectory = Path.GetDirectoryName(targetFile);

            if (!string.IsNullOrEmpty(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            await CopyFileAsync(
                file,
                targetFile,
                cancellationToken);
        }
    }

    private static async Task CopyFileAsync(
        string sourceFile,
        string destinationFile,
        CancellationToken cancellationToken)
    {
        await using var source = new FileStream(
            sourceFile,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 64,
            useAsync: true);

        await using var destination = new FileStream(
            destinationFile,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 1024 * 64,
            useAsync: true);

        await source.CopyToAsync(
            destination,
            cancellationToken);
    }
}