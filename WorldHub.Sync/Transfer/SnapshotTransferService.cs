using System.IO;
using WorldHub.Core.Entities;
using WorldHub.Core.Interfaces;
using WorldHub.Core.Rules;
using WorldHub.Sync.Services;

namespace WorldHub.Sync.Transfer;

public sealed class SnapshotTransferService
{
    private readonly ISnapshotRepository _snapshotRepository;
    private readonly SnapshotService _snapshotService;
    private readonly IWorldRepository _worldRepository;

    public SnapshotTransferService(
        ISnapshotRepository snapshotRepository,
        SnapshotService snapshotService,
        IWorldRepository worldRepository)
    {
        ArgumentNullException.ThrowIfNull(snapshotRepository);
        ArgumentNullException.ThrowIfNull(snapshotService);
        ArgumentNullException.ThrowIfNull(worldRepository);

        _snapshotRepository = snapshotRepository;
        _snapshotService = snapshotService;
        _worldRepository = worldRepository;
    }

    public async Task<SnapshotTransferPlan> PrepareForPushAsync(
        World world,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (world.CurrentSnapshotId <= 0)
        {
            throw new InvalidOperationException(
                $"World '{world.Name}' does not have a current snapshot.");
        }

        var history = await _snapshotRepository.GetByWorldIdAsync(
            world.Id,
            cancellationToken);

        var snapshots = history
            .OrderByDescending(s => s.Version)
            .Take(SnapshotRetentionRule.MaxLocalSnapshots)
            .OrderBy(s => s.Version)
            .ToArray();

        if (snapshots.Length == 0)
        {
            throw new InvalidOperationException(
                $"World '{world.Name}' does not have snapshots.");
        }

        if (!CurrentSnapshotInHistoryRule.IsSatisfied(world, snapshots))
        {
            throw new InvalidOperationException(
                $"Current snapshot '{world.CurrentSnapshotId}' was not found in world history.");
        }

        var items = new List<SnapshotTransferItem>(snapshots.Length);
        long totalBytes = 0;

        foreach (var snapshot in snapshots)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!Directory.Exists(snapshot.StoragePath))
            {
                throw new DirectoryNotFoundException(
                    $"Snapshot storage was not found: {snapshot.StoragePath}");
            }

            var files = BuildFileList(snapshot.StoragePath, cancellationToken);
            var snapshotBytes = files.Sum(f => f.Length);

            items.Add(new SnapshotTransferItem
            {
                Snapshot = snapshot,
                Files = files,
                TotalBytes = snapshotBytes
            });

            totalBytes += snapshotBytes;
        }

        return new SnapshotTransferPlan
        {
            World = world,
            Items = items,
            TotalBytes = totalBytes
        };
    }

    public async Task<IReadOnlyList<Snapshot>> ImportStagedSnapshotsAsync(
        World world,
        IReadOnlyList<StagedSnapshot> staged,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(staged);

        if (staged.Count == 0)
        {
            return Array.Empty<Snapshot>();
        }

        var latest = staged
            .OrderByDescending(s => s.SnapshotVersion)
            .First();

        if (!WorldPathRule.IsSame(
                Path.GetFullPath(world.LocalPath),
                Path.GetFullPath(latest.StagedPath)))
        {
            await ReplaceDirectoryAsync(
                latest.StagedPath,
                world.LocalPath,
                cancellationToken);
        }

        await _snapshotService.DeleteByWorldIdAsync(
            world.Id,
            cancellationToken);

        var imported = new List<Snapshot>(staged.Count);

        foreach (var item in staged.OrderBy(s => s.SnapshotVersion))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var snapshot = new Snapshot
            {
                Id = item.SnapshotId,
                WorldId = world.Id,
                Version = item.SnapshotVersion,
                ParentSnapshotId = item.ParentSnapshotId,
                AuthorId = item.AuthorId,
                Message = item.Message,
                WorldHash = item.WorldHash,
                StoragePath = string.Empty,
                CreatedAt = item.CreatedAt
            };

            var result = await _snapshotService.ImportAsync(
                world,
                snapshot,
                item.StagedPath,
                cancellationToken);

            imported.Add(result);
        }

        world.CurrentSnapshotId = latest.SnapshotId;
        world.UpdatedAt = DateTime.UtcNow;

        await _worldRepository.UpdateAsync(world, cancellationToken);

        return imported;
    }

    public Task ReplaceWorldWithStagingAsync(
        string sourceDirectory,
        string targetDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);

        return ReplaceDirectoryAsync(
            sourceDirectory,
            targetDirectory,
            cancellationToken);
    }

    public string SanitizeWorldName(string worldName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worldName);

        var invalidCharacters = Path.GetInvalidFileNameChars();

        var result = new string(
                worldName
                    .Select(c => invalidCharacters.Contains(c) ? '_' : c)
                    .ToArray())
            .Trim();

        return string.IsNullOrWhiteSpace(result)
            ? "ReceivedWorld"
            : result;
    }

    public string EnsureUniqueDirectoryPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!Directory.Exists(path))
        {
            return path;
        }

        for (var index = 2; index <= 9999; index++)
        {
            var candidate = $"{path}_{index}";

            if (!Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException(
            $"Unable to find a free directory name for '{path}'.");
    }

    private static IReadOnlyList<TransferFileItem> BuildFileList(
        string rootPath,
        CancellationToken cancellationToken)
    {
        var files = new List<TransferFileItem>();

        foreach (var filePath in Directory.EnumerateFiles(
                     rootPath,
                     "*",
                     SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(rootPath, filePath);
            var fileInfo = new FileInfo(filePath);

            files.Add(new TransferFileItem
            {
                FullPath = filePath,
                RelativePath = relativePath,
                Length = fileInfo.Length
            });
        }

        return files;
    }

    private static async Task ReplaceDirectoryAsync(
        string sourceDirectory,
        string targetDirectory,
        CancellationToken cancellationToken)
    {
        var backupDirectory = $"{targetDirectory}.worldhub-pull-backup";

        if (Directory.Exists(backupDirectory))
        {
            Directory.Delete(backupDirectory, recursive: true);
        }

        var backupCreated = false;

        try
        {
            if (Directory.Exists(targetDirectory))
            {
                await CopyDirectoryAsync(
                    targetDirectory,
                    backupDirectory,
                    cancellationToken);

                backupCreated = true;

                Directory.Delete(targetDirectory, recursive: true);
            }

            await CopyDirectoryAsync(
                sourceDirectory,
                targetDirectory,
                cancellationToken);

            if (backupCreated && Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, recursive: true);
            }
        }
        catch
        {
            if (Directory.Exists(targetDirectory))
            {
                Directory.Delete(targetDirectory, recursive: true);
            }

            if (backupCreated && Directory.Exists(backupDirectory))
            {
                Directory.Move(backupDirectory, targetDirectory);
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

            var relativePath = Path.GetRelativePath(sourceDirectory, directory);

            Directory.CreateDirectory(
                Path.Combine(destinationDirectory, relativePath));
        }

        foreach (var file in Directory.EnumerateFiles(
                     sourceDirectory,
                     "*",
                     SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            var targetFile = Path.Combine(destinationDirectory, relativePath);
            var directory = Path.GetDirectoryName(targetFile);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var source = new FileStream(
                file,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                64 * 1024,
                useAsync: true);

            await using var destination = new FileStream(
                targetFile,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                64 * 1024,
                useAsync: true);

            await source.CopyToAsync(destination, cancellationToken);
        }
    }
}