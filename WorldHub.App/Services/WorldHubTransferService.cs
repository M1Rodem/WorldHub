using System.IO;
using WorldHub.Core.Entities;
using WorldHub.Core.Interfaces;
using WorldHub.Network.Models;
using WorldHub.Network.Protocol;
using WorldHub.Network.Services;
using WorldHub.Sync.Services;

namespace WorldHub.App.Services;

public sealed class WorldHubTransferService
{
    private readonly NetworkService _networkService;
    private readonly ISnapshotRepository _snapshotRepository;
    private readonly WorldService _worldService;
    private readonly SnapshotService _snapshotService;
    private readonly string _receivedWorldsRootPath;
    private readonly Guid _localPlayerId;

    public WorldHubTransferService(
        NetworkService networkService,
        ISnapshotRepository snapshotRepository,
        WorldService worldService,
        SnapshotService snapshotService,
        string receivedWorldsRootPath,
        Guid localPlayerId)
    {
        ArgumentNullException.ThrowIfNull(networkService);
        ArgumentNullException.ThrowIfNull(snapshotRepository);
        ArgumentNullException.ThrowIfNull(worldService);
        ArgumentNullException.ThrowIfNull(snapshotService);
        ArgumentException.ThrowIfNullOrWhiteSpace(receivedWorldsRootPath);

        if (localPlayerId == Guid.Empty)
        {
            throw new ArgumentException(
                "Local player ID cannot be empty.",
                nameof(localPlayerId));
        }

        _networkService = networkService;
        _snapshotRepository = snapshotRepository;
        _worldService = worldService;
        _snapshotService = snapshotService;
        _receivedWorldsRootPath =
            Path.GetFullPath(receivedWorldsRootPath);
        _localPlayerId = localPlayerId;

        Directory.CreateDirectory(
            _receivedWorldsRootPath);
    }

    public async Task PushAsync(
        World world,
        string host,
        int port,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(
                nameof(port));
        }

        if (world.CurrentSnapshotId <= 0)
        {
            throw new InvalidOperationException(
                $"World '{world.Name}' does not have a current snapshot.");
        }

        var snapshot =
            await _snapshotRepository.GetByIdAsync(
                world.CurrentSnapshotId,
                cancellationToken);

        if (snapshot is null)
        {
            throw new InvalidOperationException(
                $"Current snapshot '{world.CurrentSnapshotId}' was not found.");
        }

        if (!Directory.Exists(snapshot.StoragePath))
        {
            throw new DirectoryNotFoundException(
                $"Snapshot storage was not found: {snapshot.StoragePath}");
        }

        var files =
            BuildFileList(
                snapshot.StoragePath,
                cancellationToken);

        var totalBytes =
            files.Sum(static file => file.Length);

        await using var connection =
            await _networkService.ConnectAsync(
                host,
                port,
                cancellationToken);

        await WorldTransferProtocol.SendMessageAsync(
            connection,
            WorldTransferProtocol.PushRequest,
            cancellationToken);

        await WorldTransferProtocol.SendJsonAsync(
            connection,
            new PushRequest(
                world.Id,
                snapshot.Id),
            cancellationToken);

        var metadata =
            new SnapshotTransferMetadata(
                world.Id,
                world.Name,
                world.MinecraftVersion,
                world.Loader,
                world.LoaderVersion,
                world.ModpackHash,
                snapshot.Id,
                snapshot.Version,
                snapshot.WorldHash,
                snapshot.Message,
                snapshot.CreatedAt,
                totalBytes,
                files.Count);

        await WorldTransferProtocol.SendJsonAsync(
            connection,
            metadata,
            cancellationToken);

        long transferredBytes = 0;

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await WorldTransferProtocol.SendJsonAsync(
                connection,
                new SnapshotFileMetadata(
                    file.RelativePath,
                    file.Length),
                cancellationToken);

            await using var source =
                new FileStream(
                    file.FullPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    64 * 1024,
                    useAsync: true);

            var fileProgress =
                new Progress<long>(
                    fileTransferred =>
                    {
                        progress?.Report(
                            transferredBytes + fileTransferred);
                    });

            await WorldTransferProtocol.SendFileAsync(
                connection,
                source,
                file.Length,
                fileProgress,
                cancellationToken);

            transferredBytes += file.Length;

            progress?.Report(
                transferredBytes);
        }

        await WorldTransferProtocol.SendMessageAsync(
            connection,
            WorldTransferProtocol.TransferCompleted,
            cancellationToken);

        var response =
            await WorldTransferProtocol.ReceiveMessageAsync(
                connection,
                cancellationToken);

        if (response !=
            WorldTransferProtocol.TransferAccepted)
        {
            throw new InvalidOperationException(
                $"Remote WorldHub rejected the transfer: {response}");
        }
    }

    public async Task PullAsync(
        World world,
        string host,
        int port,
        string? targetPath = null,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(
                nameof(port));
        }

        var resolvedTargetPath =
            targetPath;

        if (string.IsNullOrWhiteSpace(resolvedTargetPath))
        {
            if (!IsTemporaryReceivedWorldPath(
                    world.LocalPath))
            {
                resolvedTargetPath =
                    world.LocalPath;
            }
        }

        await using var connection =
            await _networkService.ConnectAsync(
                host,
                port,
                cancellationToken);

        await WorldTransferProtocol.SendMessageAsync(
            connection,
            WorldTransferProtocol.PullRequest,
            cancellationToken);

        await WorldTransferProtocol.SendJsonAsync(
            connection,
            new PullRequest(
                world.Name),
            cancellationToken);

        var response =
            await WorldTransferProtocol.ReceiveMessageAsync(
                connection,
                cancellationToken);

        if (response ==
            WorldTransferProtocol.TransferRejected)
        {
            throw new InvalidOperationException(
                "The remote WorldHub does not have this world.");
        }

        if (response !=
            WorldTransferProtocol.SnapshotMetadata)
        {
            throw new InvalidDataException(
                $"Unexpected pull response: {response}");
        }

        var metadata =
            await WorldTransferProtocol.ReceiveJsonAsync<
                SnapshotTransferMetadata>(
                connection,
                cancellationToken);

        if (!string.Equals(
                metadata.WorldName,
                world.Name,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Received world does not match the requested world.");
        }

        var temporaryDirectory =
            Path.Combine(
                _receivedWorldsRootPath,
                $"pull-{Guid.NewGuid():N}");

        Directory.CreateDirectory(
            temporaryDirectory);

        try
        {
            long receivedBytes = 0;

            for (var index = 0;
                 index < metadata.FileCount;
                 index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fileMetadata =
                    await WorldTransferProtocol.ReceiveJsonAsync<
                        SnapshotFileMetadata>(
                        connection,
                        cancellationToken);

                var relativePath =
                    ValidateRelativePath(
                        fileMetadata.RelativePath);

                if (fileMetadata.Length < 0)
                {
                    throw new InvalidDataException(
                        "Received file has an invalid length.");
                }

                var targetFile =
                    Path.Combine(
                        temporaryDirectory,
                        relativePath);

                var targetDirectory =
                    Path.GetDirectoryName(targetFile);

                if (string.IsNullOrWhiteSpace(targetDirectory))
                {
                    throw new InvalidDataException(
                        "Invalid received file path.");
                }

                Directory.CreateDirectory(
                    targetDirectory);

                await using var destination =
                    new FileStream(
                        targetFile,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        64 * 1024,
                        useAsync: true);

                var fileProgress =
                    new Progress<long>(
                        fileTransferred =>
                        {
                            progress?.Report(
                                receivedBytes + fileTransferred);
                        });

                await WorldTransferProtocol.ReceiveFileAsync(
                    connection,
                    destination,
                    fileMetadata.Length,
                    fileProgress,
                    cancellationToken);

                receivedBytes += fileMetadata.Length;
            }

            if (receivedBytes != metadata.TotalBytes)
            {
                throw new InvalidDataException(
                    $"Pull size mismatch. " +
                    $"Expected {metadata.TotalBytes} bytes, " +
                    $"received {receivedBytes} bytes.");
            }

            var completedMessage =
                await WorldTransferProtocol.ReceiveMessageAsync(
                    connection,
                    cancellationToken);

            if (completedMessage !=
                WorldTransferProtocol.TransferCompleted)
            {
                throw new InvalidDataException(
                    $"Unexpected transfer completion message: " +
                    $"'{completedMessage}'.");
            }

            if (string.IsNullOrWhiteSpace(resolvedTargetPath))
            {
                throw new InvalidOperationException(
                    "A Minecraft world path has not been configured.");
            }

            var normalizedTargetPath =
                Path.GetFullPath(
                    resolvedTargetPath);

            if (IsPathUsedByAnotherWorld(
                    world.Id,
                    normalizedTargetPath,
                    await _worldService.GetAllAsync(
                        cancellationToken)))
            {
                throw new InvalidOperationException(
                    "The selected Minecraft world is already registered " +
                    "in another WorldHub world.");
            }

            if (!string.Equals(
                    Path.GetFullPath(world.LocalPath),
                    normalizedTargetPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                world.LocalPath =
                    normalizedTargetPath;

                await _worldService.UpdateAsync(
                    world,
                    cancellationToken);
            }

            await RestoreDirectoryAsync(
                temporaryDirectory,
                normalizedTargetPath,
                cancellationToken);

            var snapshot =
                await _snapshotService.CreateAsync(
                    world,
                    _localPlayerId,
                    $"Pulled from WorldHub: {metadata.Message}",
                    cancellationToken);

            DebugConsole.Log(
                $"Pull completed. World='{world.Name}', " +
                $"snapshot={snapshot.Id}, " +
                $"target='{normalizedTargetPath}'.");

            await WorldTransferProtocol.SendMessageAsync(
                connection,
                WorldTransferProtocol.TransferAccepted,
                cancellationToken);
        }
        catch
        {
            throw;
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(
                    temporaryDirectory,
                    recursive: true);
            }
        }
    }

    public async Task HandleIncomingAsync(
        NetworkConnection connection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var command =
            await WorldTransferProtocol.ReceiveMessageAsync(
                connection,
                cancellationToken);

        switch (command)
        {
            case WorldTransferProtocol.PushRequest:
                await HandlePushAsync(
                    connection,
                    cancellationToken);
                return;

            case WorldTransferProtocol.PullRequest:
                await HandlePullAsync(
                    connection,
                    cancellationToken);
                return;

            default:
                await WorldTransferProtocol.SendMessageAsync(
                    connection,
                    WorldTransferProtocol.TransferRejected,
                    cancellationToken);

                throw new InvalidDataException(
                    $"Unsupported transfer command: '{command}'.");
        }
    }

    private async Task HandlePushAsync(
        NetworkConnection connection,
        CancellationToken cancellationToken)
    {
        var request =
            await WorldTransferProtocol.ReceiveJsonAsync<PushRequest>(
                connection,
                cancellationToken);

        var metadata =
            await WorldTransferProtocol.ReceiveJsonAsync<
                SnapshotTransferMetadata>(
                connection,
                cancellationToken);

        if (request.WorldId == Guid.Empty ||
            request.SnapshotId <= 0 ||
            metadata.WorldId != request.WorldId ||
            metadata.SnapshotId != request.SnapshotId)
        {
            throw new InvalidDataException(
                "Invalid push request or transfer metadata.");
        }

        var temporaryDirectory =
            Path.Combine(
                _receivedWorldsRootPath,
                $"push-{Guid.NewGuid():N}");

        Directory.CreateDirectory(
            temporaryDirectory);

        try
        {
            long receivedBytes = 0;

            for (var index = 0;
                 index < metadata.FileCount;
                 index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fileMetadata =
                    await WorldTransferProtocol.ReceiveJsonAsync<
                        SnapshotFileMetadata>(
                        connection,
                        cancellationToken);

                var relativePath =
                    ValidateRelativePath(
                        fileMetadata.RelativePath);

                if (fileMetadata.Length < 0)
                {
                    throw new InvalidDataException(
                        "Received file has an invalid length.");
                }

                var targetFile =
                    Path.Combine(
                        temporaryDirectory,
                        relativePath);

                var targetDirectory =
                    Path.GetDirectoryName(targetFile);

                if (string.IsNullOrWhiteSpace(targetDirectory))
                {
                    throw new InvalidDataException(
                        "Invalid received file path.");
                }

                Directory.CreateDirectory(
                    targetDirectory);

                await using var destination =
                    new FileStream(
                        targetFile,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        64 * 1024,
                        useAsync: true);

                await WorldTransferProtocol.ReceiveFileAsync(
                    connection,
                    destination,
                    fileMetadata.Length,
                    cancellationToken: cancellationToken);

                receivedBytes += fileMetadata.Length;
            }

            if (receivedBytes != metadata.TotalBytes)
            {
                throw new InvalidDataException(
                    $"Push size mismatch. " +
                    $"Expected {metadata.TotalBytes} bytes, " +
                    $"received {receivedBytes} bytes.");
            }

            var completedMessage =
                await WorldTransferProtocol.ReceiveMessageAsync(
                    connection,
                    cancellationToken);

            if (completedMessage !=
                WorldTransferProtocol.TransferCompleted)
            {
                throw new InvalidDataException(
                    $"Unexpected push completion message: " +
                    $"'{completedMessage}'.");
            }

            var worlds =
                await _worldService.GetAllAsync(
                    cancellationToken);

            var world =
                worlds.FirstOrDefault(
                    candidate =>
                        string.Equals(
                            candidate.Name,
                            metadata.WorldName,
                            StringComparison.OrdinalIgnoreCase));

            if (world is null)
            {
                var localPath =
                    Path.Combine(
                        _receivedWorldsRootPath,
                        SanitizeWorldName(
                            metadata.WorldName));

                localPath =
                    EnsureUniqueDirectoryPath(
                        localPath);

                Directory.Move(
                    temporaryDirectory,
                    localPath);

                temporaryDirectory = string.Empty;

                world =
                    await _worldService.CreateAsync(
                        metadata.WorldName,
                        localPath,
                        metadata.MinecraftVersion,
                        metadata.Loader,
                        metadata.LoaderVersion,
                        metadata.ModpackHash,
                        cancellationToken);

                await _snapshotService.CreateAsync(
                    world,
                    _localPlayerId,
                    $"Received from WorldHub: {metadata.Message}",
                    cancellationToken);
            }
            else
            {
                var stagingWorld =
                    Path.Combine(
                        _receivedWorldsRootPath,
                        $"staging-{Guid.NewGuid():N}");

                Directory.Move(
                    temporaryDirectory,
                    stagingWorld);

                temporaryDirectory = string.Empty;

                try
                {
                    if (IsTemporaryReceivedWorldPath(
                            world.LocalPath))
                    {
                        await ReplaceDirectoryAsync(
                            stagingWorld,
                            world.LocalPath,
                            cancellationToken);

                        Directory.Delete(
                            stagingWorld,
                            recursive: true);

                        world.UpdatedAt = DateTime.UtcNow;

                        await _worldService.UpdateAsync(
                            world,
                            cancellationToken);
                    }
                    else
                    {
                        var snapshotWorld =
                            CreateTemporaryWorld(
                                world,
                                stagingWorld);

                        await _snapshotService.CreateAsync(
                            snapshotWorld,
                            _localPlayerId,
                            $"Received from WorldHub: {metadata.Message}",
                            cancellationToken);
                    }
                }
                finally
                {
                    if (Directory.Exists(stagingWorld))
                    {
                        Directory.Delete(
                            stagingWorld,
                            recursive: true);
                    }
                }
            }

            await WorldTransferProtocol.SendMessageAsync(
                connection,
                WorldTransferProtocol.TransferAccepted,
                cancellationToken);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(temporaryDirectory) &&
                Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(
                    temporaryDirectory,
                    recursive: true);
            }
        }
    }

    private async Task HandlePullAsync(
        NetworkConnection connection,
        CancellationToken cancellationToken)
    {
        var request =
            await WorldTransferProtocol.ReceiveJsonAsync<PullRequest>(
                connection,
                cancellationToken);

        var worlds =
            await _worldService.GetAllAsync(
                cancellationToken);

        var world =
            worlds.FirstOrDefault(
                candidate =>
                    string.Equals(
                        candidate.Name,
                        request.WorldName,
                        StringComparison.OrdinalIgnoreCase));

        if (world is null ||
            world.CurrentSnapshotId <= 0)
        {
            await WorldTransferProtocol.SendMessageAsync(
                connection,
                WorldTransferProtocol.TransferRejected,
                cancellationToken);

            return;
        }

        var snapshot =
            await _snapshotRepository.GetByIdAsync(
                world.CurrentSnapshotId,
                cancellationToken);

        if (snapshot is null ||
            !Directory.Exists(snapshot.StoragePath))
        {
            await WorldTransferProtocol.SendMessageAsync(
                connection,
                WorldTransferProtocol.TransferRejected,
                cancellationToken);

            return;
        }

        var files =
            BuildFileList(
                snapshot.StoragePath,
                cancellationToken);

        var totalBytes =
            files.Sum(static file => file.Length);

        await WorldTransferProtocol.SendMessageAsync(
            connection,
            WorldTransferProtocol.SnapshotMetadata,
            cancellationToken);

        await WorldTransferProtocol.SendJsonAsync(
            connection,
            new SnapshotTransferMetadata(
                world.Id,
                world.Name,
                world.MinecraftVersion,
                world.Loader,
                world.LoaderVersion,
                world.ModpackHash,
                snapshot.Id,
                snapshot.Version,
                snapshot.WorldHash,
                snapshot.Message,
                snapshot.CreatedAt,
                totalBytes,
                files.Count),
            cancellationToken);

        foreach (var file in files)
        {
            await WorldTransferProtocol.SendJsonAsync(
                connection,
                new SnapshotFileMetadata(
                    file.RelativePath,
                    file.Length),
                cancellationToken);

            await using var source =
                new FileStream(
                    file.FullPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    64 * 1024,
                    useAsync: true);

            await WorldTransferProtocol.SendFileAsync(
                connection,
                source,
                file.Length,
                cancellationToken: cancellationToken);
        }

        await WorldTransferProtocol.SendMessageAsync(
            connection,
            WorldTransferProtocol.TransferCompleted,
            cancellationToken);

        var response =
            await WorldTransferProtocol.ReceiveMessageAsync(
                connection,
                cancellationToken);

        if (response !=
            WorldTransferProtocol.TransferAccepted)
        {
            throw new InvalidOperationException(
                $"Pull receiver rejected the snapshot: {response}");
        }
    }

    private static World CreateTemporaryWorld(
        World existingWorld,
        string stagingPath)
    {
        return new World
        {
            Id = existingWorld.Id,
            Name = existingWorld.Name,
            LocalPath = stagingPath,
            MinecraftVersion = existingWorld.MinecraftVersion,
            Loader = existingWorld.Loader,
            LoaderVersion = existingWorld.LoaderVersion,
            ModpackHash = existingWorld.ModpackHash,
            CurrentSnapshotId = 0,
            Status = existingWorld.Status,
            OwnerId = existingWorld.OwnerId,
            CreatedAt = existingWorld.CreatedAt,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private bool IsTemporaryReceivedWorldPath(
        string path)
    {
        var fullPath =
            Path.GetFullPath(path);

        var root =
            Path.GetFullPath(
                _receivedWorldsRootPath);

        return fullPath.StartsWith(
            root + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPathUsedByAnotherWorld(
        Guid worldId,
        string path,
        IReadOnlyCollection<World> worlds)
    {
        return worlds.Any(
            world =>
                world.Id != worldId &&
                string.Equals(
                    Path.GetFullPath(world.LocalPath),
                    path,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static List<TransferFile> BuildFileList(
        string rootPath,
        CancellationToken cancellationToken)
    {
        var files = new List<TransferFile>();

        foreach (var filePath in Directory.EnumerateFiles(
                     rootPath,
                     "*",
                     SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath =
                Path.GetRelativePath(
                    rootPath,
                    filePath);

            var fileInfo =
                new FileInfo(filePath);

            files.Add(
                new TransferFile(
                    filePath,
                    relativePath,
                    fileInfo.Length));
        }

        return files;
    }

    private static async Task RestoreDirectoryAsync(
        string sourceDirectory,
        string targetDirectory,
        CancellationToken cancellationToken)
    {
        var backupDirectory =
            $"{targetDirectory}.worldhub-pull-backup";

        if (Directory.Exists(backupDirectory))
        {
            Directory.Delete(
                backupDirectory,
                recursive: true);
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

                Directory.Delete(
                    targetDirectory,
                    recursive: true);
            }

            await CopyDirectoryAsync(
                sourceDirectory,
                targetDirectory,
                cancellationToken);

            if (backupCreated &&
                Directory.Exists(backupDirectory))
            {
                Directory.Delete(
                    backupDirectory,
                    recursive: true);
            }
        }
        catch
        {
            if (Directory.Exists(targetDirectory))
            {
                Directory.Delete(
                    targetDirectory,
                    recursive: true);
            }

            if (backupCreated &&
                Directory.Exists(backupDirectory))
            {
                Directory.Move(
                    backupDirectory,
                    targetDirectory);
            }

            throw;
        }
    }

    private static async Task ReplaceDirectoryAsync(
        string sourceDirectory,
        string targetDirectory,
        CancellationToken cancellationToken)
    {
        await RestoreDirectoryAsync(
            sourceDirectory,
            targetDirectory,
            cancellationToken);
    }

    private static async Task CopyDirectoryAsync(
        string sourceDirectory,
        string destinationDirectory,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(
            destinationDirectory);

        foreach (var directory in Directory.EnumerateDirectories(
                     sourceDirectory,
                     "*",
                     SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath =
                Path.GetRelativePath(
                    sourceDirectory,
                    directory);

            Directory.CreateDirectory(
                Path.Combine(
                    destinationDirectory,
                    relativePath));
        }

        foreach (var file in Directory.EnumerateFiles(
                     sourceDirectory,
                     "*",
                     SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath =
                Path.GetRelativePath(
                    sourceDirectory,
                    file);

            var targetFile =
                Path.Combine(
                    destinationDirectory,
                    relativePath);

            var directory =
                Path.GetDirectoryName(targetFile);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(
                    directory);
            }

            await using var source =
                new FileStream(
                    file,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    64 * 1024,
                    useAsync: true);

            await using var destination =
                new FileStream(
                    targetFile,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    64 * 1024,
                    useAsync: true);

            await source.CopyToAsync(
                destination,
                cancellationToken);
        }
    }

    private static string ValidateRelativePath(
        string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new InvalidDataException(
                "Received file path is empty.");
        }

        var normalized =
            relativePath.Replace(
                '\\',
                Path.DirectorySeparatorChar)
            .Replace(
                '/',
                Path.DirectorySeparatorChar);

        if (Path.IsPathRooted(normalized))
        {
            throw new InvalidDataException(
                "Received file path must be relative.");
        }

        var segments =
            normalized.Split(
                Path.DirectorySeparatorChar,
                StringSplitOptions.RemoveEmptyEntries);

        if (segments.Any(
                static segment =>
                    segment is "." or ".."))
        {
            throw new InvalidDataException(
                "Received file path contains invalid traversal segments.");
        }

        return normalized;
    }

    private static string SanitizeWorldName(
        string worldName)
    {
        var invalidCharacters =
            Path.GetInvalidFileNameChars();

        var result =
            new string(
                worldName
                    .Select(
                        character =>
                            invalidCharacters.Contains(character)
                                ? '_'
                                : character)
                    .ToArray())
                .Trim();

        return string.IsNullOrWhiteSpace(result)
            ? "ReceivedWorld"
            : result;
    }

    private static string EnsureUniqueDirectoryPath(
        string path)
    {
        if (!Directory.Exists(path))
        {
            return path;
        }

        for (var index = 2; index <= 9999; index++)
        {
            var candidate =
                $"{path}_{index}";

            if (!Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException(
            $"Unable to find a free directory name for '{path}'.");
    }

    private sealed record TransferFile(
        string FullPath,
        string RelativePath,
        long Length);
}