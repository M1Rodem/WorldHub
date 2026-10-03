using WorldHub.Core.Diagnostics;
using WorldHub.Core.Entities;
using WorldHub.Core.Rules;
using WorldHub.Network.Models;
using WorldHub.Network.Protocol;
using WorldHub.Network.Services;
using WorldHub.Network.Transfer;
using WorldHub.Sync.Services;

namespace WorldHub.Sync.Transfer;

internal sealed class WorldTransferReceiver
{
    private readonly NetworkService _networkService;
    private readonly WorldService _worldService;
    private readonly SnapshotService _snapshotService;
    private readonly SnapshotTransferService _snapshotTransferService;
    private readonly string _receivedWorldsRootPath;
    private readonly Guid _localPlayerId;
    private readonly Func<string, int, Task<bool>> _confirmTransfer;

    public WorldTransferReceiver(
    NetworkService networkService,
    WorldService worldService,
    SnapshotService snapshotService,
    SnapshotTransferService snapshotTransferService,
    string receivedWorldsRootPath,
    Guid localPlayerId,
    Func<string, int, Task<bool>> confirmTransfer)
    {
        _networkService = networkService;
        _worldService = worldService;
        _snapshotService = snapshotService;
        _snapshotTransferService = snapshotTransferService;
        _receivedWorldsRootPath = Path.GetFullPath(receivedWorldsRootPath);
        _localPlayerId = localPlayerId;
        _confirmTransfer = confirmTransfer;

        Directory.CreateDirectory(_receivedWorldsRootPath);
        ArgumentNullException.ThrowIfNull(confirmTransfer);
    }

    // ─────────────────────────────────────────────────────────
    // Pull (client-side receiving)
    // ─────────────────────────────────────────────────────────

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
        ValidatePort(port);

        var resolvedTargetPath = ResolveTargetPath(world, targetPath);

        await using var connection = await _networkService.ConnectAsync(
            host, port, cancellationToken);

        await WorldTransferProtocol.SendMessageAsync(
            connection, WorldTransferProtocol.PullRequest, cancellationToken);

        await WorldTransferProtocol.SendJsonAsync(
            connection, new PullRequest(world.Name), cancellationToken);

        var response = await WorldTransferProtocol.ReceiveMessageAsync(
            connection, cancellationToken);

        if (response == WorldTransferProtocol.TransferRejected)
        {
            throw new InvalidOperationException(
                "The remote WorldHub does not have this world.");
        }

        if (response != WorldTransferProtocol.SnapshotMetadata)
        {
            throw new InvalidDataException(
                $"Unexpected pull response: {response}");
        }

        var metadata = await WorldTransferProtocol
            .ReceiveJsonAsync<SnapshotTransferMetadata>(connection, cancellationToken);

        if (!string.Equals(metadata.WorldName, world.Name, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Received world does not match the requested world.");
        }

        using var staging = new TransferStagingArea(_receivedWorldsRootPath, "pull");

        await SnapshotFileReceiver.ReceiveFilesAsync(
            connection,
            staging.RootPath,
            metadata.FileCount,
            metadata.TotalBytes,
            progress,
            alreadyTransferred: 0,
            cancellationToken);

        var completedMessage = await WorldTransferProtocol.ReceiveMessageAsync(
            connection, cancellationToken);

        if (completedMessage != WorldTransferProtocol.TransferCompleted)
        {
            throw new InvalidDataException(
                $"Unexpected transfer completion message: '{completedMessage}'.");
        }

        if (string.IsNullOrWhiteSpace(resolvedTargetPath))
        {
            throw new InvalidOperationException(
                "A Minecraft world path has not been configured.");
        }

        await FinalizePullAsync(
            world, staging.RootPath, resolvedTargetPath, metadata, cancellationToken);

        await WorldTransferProtocol.SendMessageAsync(
            connection, WorldTransferProtocol.TransferAccepted, cancellationToken);
    }

    // ─────────────────────────────────────────────────────────
    // HandlePush (server-side receiving)
    // ─────────────────────────────────────────────────────────

    public async Task HandlePushAsync(
    NetworkConnection connection,
    CancellationToken cancellationToken)
    {
        var request = await WorldTransferProtocol
            .ReceiveJsonAsync<PushRequest>(
                connection,
                cancellationToken);

        if (request.WorldId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.WorldName) ||
            !TransferSnapshotCountRule.IsValid(request.SnapshotCount))
        {
            throw new InvalidDataException(
                "Invalid push request.");
        }

        var accepted = await _confirmTransfer(
            request.WorldName,
            request.SnapshotCount);

        if (!accepted)
        {
            await WorldTransferProtocol.SendMessageAsync(
                connection,
                WorldTransferProtocol.TransferRejected,
                cancellationToken);

            DebugConsole.Log(
                $"Push rejected by user. " +
                $"World='{request.WorldName}', " +
                $"remoteWorld={request.WorldId}.");

            return;
        }

        await WorldTransferProtocol.SendMessageAsync(
            connection,
            WorldTransferProtocol.TransferApproved,
            cancellationToken);

        using var staging = new TransferStagingArea(
            _receivedWorldsRootPath,
            "push");

        var receivedSnapshots =
            new List<ReceivedSnapshot>(
                request.SnapshotCount);

        for (var index = 0;
             index < request.SnapshotCount;
             index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var metadata =
                await WorldTransferProtocol
                    .ReceiveJsonAsync<SnapshotTransferMetadata>(
                        connection,
                        cancellationToken);

            ValidateMetadata(
                metadata,
                request.WorldId);

            var snapshotDir =
                staging.CreateSubdirectory(
                    $"snapshot-{metadata.SnapshotVersion}");

            await SnapshotFileReceiver.ReceiveFilesAsync(
                connection,
                snapshotDir,
                metadata.FileCount,
                metadata.TotalBytes,
                progress: null,
                alreadyTransferred: 0,
                cancellationToken);

            receivedSnapshots.Add(
                new ReceivedSnapshot(
                    metadata,
                    snapshotDir));
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

        if (receivedSnapshots.Count !=
            request.SnapshotCount)
        {
            throw new InvalidDataException(
                "Received snapshot count does not match " +
                "the push request.");
        }

        ValidateSnapshotsConsistent(
            receivedSnapshots,
            request.WorldId);

        var world = await ResolveOrCreateWorldAsync(
            request.WorldId,
            receivedSnapshots[0].Metadata,
            cancellationToken);

        await ImportStagedSnapshotsAsync(
            world,
            receivedSnapshots,
            cancellationToken);

        DebugConsole.Log(
            $"Push imported. World='{world.Name}', " +
            $"remoteWorld={request.WorldId}, " +
            $"snapshots={receivedSnapshots.Count}.");

        await WorldTransferProtocol.SendMessageAsync(
            connection,
            WorldTransferProtocol.TransferAccepted,
            cancellationToken);
    }

    // ─────────────────────────────────────────────────────────
    // Finalize (pull)
    // ─────────────────────────────────────────────────────────

    private async Task FinalizePullAsync(
        World world,
        string stagingPath,
        string targetPath,
        SnapshotTransferMetadata metadata,
        CancellationToken cancellationToken)
    {
        var normalizedTargetPath = Path.GetFullPath(targetPath);

        var allWorlds = await _worldService.GetAllAsync(cancellationToken);

        if (DuplicateWorldPathRule.IsUsedByAnotherWorld(
                world.Id, normalizedTargetPath, allWorlds))
        {
            throw new InvalidOperationException(
                "The selected Minecraft world is already registered " +
                "in another WorldHub world.");
        }

        if (!WorldPathRule.IsSame(
                Path.GetFullPath(world.LocalPath), normalizedTargetPath))
        {
            world.LocalPath = normalizedTargetPath;
            await _worldService.UpdateAsync(world, cancellationToken);
        }

        await _snapshotTransferService.ReplaceWorldWithStagingAsync(
            stagingPath, normalizedTargetPath, cancellationToken);

        var snapshot = await _snapshotService.CreateAsync(
            world,
            _localPlayerId,
            $"Pulled from WorldHub: {metadata.Message}",
            cancellationToken);

        DebugConsole.Log(
            $"Pull completed. World='{world.Name}', " +
            $"snapshot={snapshot.Id}, target='{normalizedTargetPath}'.");
    }

    // ─────────────────────────────────────────────────────────
    // World resolution (push)
    // ─────────────────────────────────────────────────────────

    private async Task<World> ResolveOrCreateWorldAsync(
        Guid remoteWorldId,
        SnapshotTransferMetadata metadata,
        CancellationToken cancellationToken)
    {
        var worlds = await _worldService.GetAllAsync(cancellationToken);

        var existing = RemoteWorldLinkRule.FindLinkedWorld(
            remoteWorldId, metadata.WorldName, worlds);

        if (existing is not null)
        {
            existing.RemoteWorldId = remoteWorldId;

            if (!string.Equals(existing.Name, metadata.WorldName, StringComparison.Ordinal))
            {
                existing.Name = metadata.WorldName;
            }

            existing.MinecraftVersion = metadata.MinecraftVersion;
            existing.Loader = metadata.Loader;
            existing.LoaderVersion = metadata.LoaderVersion;
            existing.ModpackHash = metadata.ModpackHash;

            await _worldService.UpdateAsync(existing, cancellationToken);
            return existing;
        }

        var localPath = Path.Combine(
            _receivedWorldsRootPath,
            _snapshotTransferService.SanitizeWorldName(metadata.WorldName));

        localPath = _snapshotTransferService.EnsureUniqueDirectoryPath(localPath);

        var created = await _worldService.CreateAsync(
            metadata.WorldName,
            localPath,
            metadata.MinecraftVersion,
            metadata.Loader,
            metadata.LoaderVersion,
            metadata.ModpackHash,
            cancellationToken);

        created.RemoteWorldId = remoteWorldId;
        await _worldService.UpdateAsync(created, cancellationToken);

        return created;
    }

    private async Task ImportStagedSnapshotsAsync(
        World world,
        IReadOnlyList<ReceivedSnapshot> receivedSnapshots,
        CancellationToken cancellationToken)
    {
        var staged = receivedSnapshots
            .OrderBy(s => s.Metadata.SnapshotVersion)
            .Select(s => new StagedSnapshot
            {
                SnapshotId = s.Metadata.SnapshotId,
                SnapshotVersion = s.Metadata.SnapshotVersion,
                ParentSnapshotId = s.Metadata.ParentSnapshotId,
                AuthorId = s.Metadata.AuthorId,
                WorldHash = s.Metadata.WorldHash,
                Message = s.Metadata.Message,
                CreatedAt = s.Metadata.CreatedAt,
                StagedPath = s.SourcePath
            })
            .ToArray();

        await _snapshotTransferService.ImportStagedSnapshotsAsync(
            world, staged, cancellationToken);
    }

    // ─────────────────────────────────────────────────────────
    // Validation
    // ─────────────────────────────────────────────────────────

    private static void ValidateMetadata(
        SnapshotTransferMetadata metadata, Guid expectedWorldId)
    {
        if (metadata.WorldId != expectedWorldId ||
            metadata.SnapshotId <= 0 ||
            metadata.SnapshotVersion <= 0 ||
            string.IsNullOrWhiteSpace(metadata.WorldName))
        {
            throw new InvalidDataException("Invalid snapshot transfer metadata.");
        }
    }

    private static void ValidateSnapshotsConsistent(
        IReadOnlyList<ReceivedSnapshot> snapshots, Guid expectedWorldId)
    {
        var first = snapshots[0].Metadata;

        if (snapshots.Any(s =>
                s.Metadata.WorldId != expectedWorldId ||
                !string.Equals(
                    s.Metadata.WorldName,
                    first.WorldName,
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException(
                "Received snapshots belong to different worlds.");
        }
    }

    // ─────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────

    private string? ResolveTargetPath(World world, string? targetPath)
    {
        if (!string.IsNullOrWhiteSpace(targetPath))
        {
            return targetPath;
        }

        if (!TransferPathValidator.IsUnderRoot(world.LocalPath, _receivedWorldsRootPath))
        {
            return world.LocalPath;
        }

        return null;
    }

    private static void ValidatePort(int port)
    {
        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port));
        }
    }

    private sealed record ReceivedSnapshot(
        SnapshotTransferMetadata Metadata,
        string SourcePath);
}